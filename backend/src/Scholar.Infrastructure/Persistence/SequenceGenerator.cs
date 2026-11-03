using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Scholar.Application.Abstractions;
using Scholar.Domain.Platform;

namespace Scholar.Infrastructure.Persistence;

/// <summary>
/// Issues the next number of a sequence for the period the document is dated in. The counter row for that period is
/// upserted with a single <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>; its row lock is held until the caller's
/// transaction ends, so concurrent callers are serialised and numbers are never duplicated, and a rolled-back
/// transaction returns its number, so sequences stay gap-free. Each period has its own counter, so back-dated
/// documents never disturb (or collide with) the current period.
/// </summary>
internal sealed class SequenceGenerator(AppDbContext db, ITenantContext tenant) : ISequenceGenerator
{
    public async Task<string> NextAsync(string key, DateOnly date, CancellationToken ct = default)
    {
        var schoolId = tenant.SchoolId ?? throw new InvalidOperationException("Sequences require a school context.");
        var defaults = SequenceKeys.Defaults.TryGetValue(key, out var d)
            ? d
            : (Prefix: key.ToUpperInvariant(), Pattern: "{prefix}-{seq:5}", Reset: SequenceResetPolicy.Never);

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await db.Database.OpenConnectionAsync(ct);
        }

        try
        {
            var transaction = db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

            await using (var insert = new NpgsqlCommand(
                             """
                             INSERT INTO number_sequences (id, school_id, key, prefix, pattern, reset_policy, created_at)
                             VALUES (@id, @school, @key, @prefix, @pattern, @reset, now())
                             ON CONFLICT (school_id, key) DO NOTHING
                             """, connection, transaction))
            {
                insert.Parameters.AddWithValue("id", Guid.CreateVersion7());
                insert.Parameters.AddWithValue("school", schoolId);
                insert.Parameters.AddWithValue("key", key);
                insert.Parameters.AddWithValue("prefix", defaults.Prefix);
                insert.Parameters.AddWithValue("pattern", defaults.Pattern);
                insert.Parameters.AddWithValue("reset", defaults.Reset.ToString());
                await insert.ExecuteNonQueryAsync(ct);
            }

            // Reading the format and bumping the period's counter happen in one statement.
            await using var next = new NpgsqlCommand(
                """
                WITH cfg AS (
                  SELECT prefix, pattern,
                         CASE reset_policy WHEN 'Yearly' THEN @year WHEN 'Monthly' THEN @month ELSE '' END AS period_key
                  FROM number_sequences WHERE school_id = @school AND key = @key
                ), bumped AS (
                  INSERT INTO number_sequence_counters AS c (id, school_id, key, period_key, next_value, created_at)
                  SELECT @id, @school, @key, cfg.period_key, 2, now() FROM cfg
                  ON CONFLICT (school_id, key, period_key) DO UPDATE SET next_value = c.next_value + 1, updated_at = now()
                  RETURNING c.next_value - 1 AS value
                )
                SELECT bumped.value, cfg.prefix, cfg.pattern FROM bumped CROSS JOIN cfg
                """, connection, transaction);
            next.Parameters.AddWithValue("id", Guid.CreateVersion7());
            next.Parameters.AddWithValue("school", schoolId);
            next.Parameters.AddWithValue("key", key);
            next.Parameters.AddWithValue("year", NumberSequence.PeriodFor(SequenceResetPolicy.Yearly, date));
            next.Parameters.AddWithValue("month", NumberSequence.PeriodFor(SequenceResetPolicy.Monthly, date));

            await using var reader = await next.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new InvalidOperationException($"Number sequence '{key}' could not be advanced.");
            }

            var value = reader.GetInt64(0);
            var prefix = reader.GetString(1);
            var pattern = reader.GetString(2);
            return SequencePattern.Format(pattern, prefix, value, date);
        }
        finally
        {
            if (openedHere)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
