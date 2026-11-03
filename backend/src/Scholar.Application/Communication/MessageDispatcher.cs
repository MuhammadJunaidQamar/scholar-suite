using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scholar.Application.Abstractions;
using Scholar.Domain.Communication;

namespace Scholar.Application.Communication;

/// <param name="Accepted">The provider took the message (it is now queued with them).</param>
public sealed record ProviderSendResult(bool Accepted, string? ProviderMessageId, string? Error);

/// <summary>
/// A connected SMS, WhatsApp or email gateway. None is registered until a school connects one; until then every
/// message is recorded honestly as not sent.
/// </summary>
public interface IMessageChannelProvider
{
    MessageChannel Channel { get; }
    string Name { get; }
    Task<ProviderSendResult> SendAsync(OutboundMessage message, CancellationToken ct);
}

/// <summary>Hands pending outbox messages to their channel's provider and records exactly what the provider said.</summary>
public sealed class MessageDispatcher(
    IAppDbContext db,
    IEnumerable<IMessageChannelProvider> providers,
    TimeProvider time,
    ILogger<MessageDispatcher> log)
{
    public static string NoProviderReason(MessageChannel channel) => channel switch
    {
        MessageChannel.Sms => "No SMS provider is connected, so the message was not sent.",
        MessageChannel.WhatsApp => "No WhatsApp provider is connected, so the message was not sent.",
        _ => "No email provider is connected, so the message was not sent.",
    };

    public bool CanSend(MessageChannel channel) => providers.Any(p => p.Channel == channel);

    /// <summary>Dispatches the given messages (those still pending); returns how many a provider accepted.</summary>
    public async Task<int> DispatchAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct)
    {
        if (messageIds.Count == 0) return 0;
        var messages = await db.OutboundMessages.Where(m => messageIds.Contains(m.Id) && m.Status == MessageStatus.Pending).ToListAsync(ct);
        var accepted = 0;
        foreach (var message in messages)
        {
            var provider = providers.FirstOrDefault(p => p.Channel == message.Channel);
            if (provider is null)
            {
                message.MarkNotSent(NoProviderReason(message.Channel), time.GetUtcNow());
                continue;
            }

            try
            {
                var result = await provider.SendAsync(message, ct);
                if (result.Accepted)
                {
                    message.MarkAccepted(provider.Name, result.ProviderMessageId, time.GetUtcNow());
                    accepted++;
                }
                else
                {
                    message.MarkFailed(provider.Name, result.Error ?? "The provider refused the message.", time.GetUtcNow());
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Provider {Provider} failed for message {MessageId}", provider.Name, message.Id);
                message.MarkFailed(provider.Name, "The provider could not be reached.", time.GetUtcNow());
            }
        }

        await db.SaveChangesAsync(ct);
        return accepted;
    }
}
