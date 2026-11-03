using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Scholar.Application.Abstractions;
using Scholar.Domain.Platform;

namespace Scholar.Infrastructure.Persistence;

/// <summary>
/// Advances <c>number_sequences</c> with a single <c>UPDATE … RETURNING</c>. The row lock is held until the caller's
/// transaction ends, so concurrent callers are serialised and numbers are never duplicated; a rolled-back transaction
/// returns its number, so sequences stay gap-free.
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
                             INSERT INTO number_sequences (id, school_id, key, prefix, pattern, reset_policy, period_key, next_value, created_at)
                             VALUES (@id, @school, @key, @prefix, @pattern, @reset, '', 1, now())
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

            var year = date.Year.ToString(CultureInfo.InvariantCulture);
            var month = date.ToString("yyyyMM", CultureInfo.InvariantCulture);
            await using var update = new NpgsqlCommand(
                """
                UPDATE number_sequences SET
                  next_value = CASE WHEN period_key = (CASE reset_policy WHEN 'Yearly' THEN @year WHEN 'Monthly' THEN @month ELSE '' END)
                               THEN next_value + 1 ELSE 2 END,
                  period_key = CASE reset_policy WHEN 'Yearly' THEN @year WHEN 'Monthly' THEN @month ELSE '' END,
                  updated_at = now()
                WHERE school_id = @school AND key = @key
                RETURNING next_value - 1, prefix, pattern
                """, connection, transaction);
            update.Parameters.AddWithValue("school", schoolId);
            update.Parameters.AddWithValue("key", key);
            update.Parameters.AddWithValue("year", year);
            update.Parameters.AddWithValue("month", month);

            await using var reader = await update.ExecuteReaderAsync(ct);
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
