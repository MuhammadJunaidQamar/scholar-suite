using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scholar.Application.Abstractions;
using Scholar.Domain.Platform;
using Scholar.Infrastructure.Persistence;
using Scholar.IntegrationTests.Infrastructure;

namespace Scholar.IntegrationTests.Persistence;

public sealed class TenancyAndSequenceTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Queries_only_see_the_current_schools_rows()
    {
        var a = await Factory.CreateSchoolWithUserAsync("alpha");
        var b = await Factory.CreateSchoolWithUserAsync("bravo");

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using (tenant.Enter(a.SchoolId))
        {
            var campuses = await db.Campuses.ToListAsync();
            Assert.All(campuses, c => Assert.Equal(a.SchoolId, c.SchoolId));
            Assert.Null(await db.Campuses.SingleOrDefaultAsync(c => c.Id == b.CampusId));
        }

        // Without a tenant nothing tenant-owned is visible at all (safe default).
        Assert.Empty(await db.Campuses.ToListAsync());
    }

    [Fact]
    public async Task Writing_another_schools_row_is_refused()
    {
        var a = await Factory.CreateSchoolWithUserAsync("alpha");
        var b = await Factory.CreateSchoolWithUserAsync("bravo");

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using (tenant.Enter(a.SchoolId))
        {
            db.Campuses.Add(new Campus(b.SchoolId, "EVIL", "Injected campus"));
            await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var foreign = await db.Campuses.IgnoreQueryFilters().SingleAsync(c => c.Id == b.CampusId);
            foreign.Name = "Renamed by another school";
            await Assert.ThrowsAsync<TenantViolationException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Changes_to_audited_entities_are_recorded()
    {
        var a = await Factory.CreateSchoolWithUserAsync("alpha");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using (tenant.Enter(a.SchoolId))
        {
            var campus = await db.Campuses.SingleAsync(c => c.Id == a.CampusId);
            campus.Name = "City Campus";
            await db.SaveChangesAsync();

            var audit = await db.AuditLogs.SingleAsync(l => l.EntityId == a.CampusId.ToString() && l.Action == "Updated");
            Assert.Contains("\"name\"", audit.Changes, StringComparison.Ordinal);
            Assert.Contains("City Campus", audit.Changes, StringComparison.Ordinal);
            Assert.NotNull(campus.UpdatedAt);
        }
    }

    [Fact]
    public async Task Sequences_are_unique_under_concurrency_and_formatted()
    {
        var a = await Factory.CreateSchoolWithUserAsync("alpha");
        var date = new DateOnly(2026, 4, 15);

        var numbers = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            var sequences = scope.ServiceProvider.GetRequiredService<ISequenceGenerator>();
            using (tenant.Enter(a.SchoolId))
            {
                return await db.InTransactionAsync(ct => sequences.NextAsync(SequenceKeys.Admission, date, ct));
            }
        }));

        Assert.Equal(25, numbers.Distinct().Count());
        Assert.Contains("ADM-2026-0001", numbers);
        Assert.Contains("ADM-2026-0025", numbers);
    }

    [Fact]
    public async Task Yearly_sequences_restart_in_a_new_year_and_rollbacks_return_the_number()
    {
        var a = await Factory.CreateSchoolWithUserAsync("alpha");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var sequences = scope.ServiceProvider.GetRequiredService<ISequenceGenerator>();

        using (tenant.Enter(a.SchoolId))
        {
            Assert.Equal("ADM-2025-0001", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2025, 8, 1)));
            Assert.Equal("ADM-2025-0002", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2025, 9, 1)));
            Assert.Equal("ADM-2026-0001", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2026, 1, 5)));

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.InTransactionAsync<string>(async ct =>
            {
                await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2026, 2, 1), ct);
                throw new InvalidOperationException("business failure after numbering");
            }));

            Assert.Equal("ADM-2026-0002", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2026, 2, 1)));
        }
    }

    [Fact]
    public async Task Back_dated_documents_draw_from_their_own_period_and_never_collide()
    {
        var a = await Factory.CreateSchoolWithUserAsync("alpha");
        await using var scope = Factory.Services.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var sequences = scope.ServiceProvider.GetRequiredService<ISequenceGenerator>();

        using (tenant.Enter(a.SchoolId))
        {
            Assert.Equal("ADM-2026-0001", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2026, 8, 3)));
            Assert.Equal("ADM-2026-0002", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2026, 8, 4)));

            // Registering a student who joined in 2024 must not restart 2026's numbering.
            Assert.Equal("ADM-2024-0001", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2024, 8, 12)));
            Assert.Equal("ADM-2026-0003", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2026, 9, 1)));
            Assert.Equal("ADM-2024-0002", await sequences.NextAsync(SequenceKeys.Admission, new DateOnly(2024, 9, 2)));

            // Monthly sequences keep one counter per month in the same way.
            Assert.Equal("APP-2611-0001", await sequences.NextAsync(SequenceKeys.Application, new DateOnly(2026, 11, 2)));
            Assert.Equal("APP-2610-0001", await sequences.NextAsync(SequenceKeys.Application, new DateOnly(2026, 10, 30)));
            Assert.Equal("APP-2611-0002", await sequences.NextAsync(SequenceKeys.Application, new DateOnly(2026, 11, 3)));
        }
    }
}
