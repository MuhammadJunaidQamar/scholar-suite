using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scholar.Application.Abstractions;
using Scholar.Application.Communication;
using Scholar.Domain.Communication;
using Scholar.Infrastructure.Persistence;

namespace Scholar.Infrastructure.Messaging;

/// <summary>
/// Outbox safety net: messages are normally dispatched right after the transaction that created them commits; any
/// left pending (the process stopped in between) are picked up here a minute later.
/// </summary>
internal sealed class PendingMessageSweeper(IServiceScopeFactory scopes, TimeProvider time, ILogger<PendingMessageSweeper> log)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Outbox sweep failed");
            }
        }
    }

    internal async Task SweepAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow().AddSeconds(-30);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Platform-level read across schools; each school's batch is then dispatched inside its own tenant.
        var pending = await db.OutboundMessages.IgnoreQueryFilters()
            .Where(m => m.Status == MessageStatus.Pending && m.CreatedAt < cutoff)
            .OrderBy(m => m.CreatedAt).Take(500)
            .Select(m => new { m.Id, m.SchoolId })
            .ToListAsync(ct);

        foreach (var school in pending.GroupBy(m => m.SchoolId))
        {
            await using var schoolScope = scopes.CreateAsyncScope();
            var tenant = schoolScope.ServiceProvider.GetRequiredService<ITenantContext>();
            using (tenant.Enter(school.Key))
            {
                await schoolScope.ServiceProvider.GetRequiredService<MessageDispatcher>()
                    .DispatchAsync(school.Select(m => m.Id).ToList(), ct);
            }
        }
    }
}
