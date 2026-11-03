using Scholar.Domain.Common;

namespace Scholar.Domain.Communication;

public enum MessageChannel { Sms, WhatsApp, Email }

/// <summary>
/// Where a message is. Only a provider moves a message past <see cref="NotSent"/>: <see cref="Queued"/> means the
/// provider accepted it, <see cref="Sent"/> that it left the provider, and <see cref="Delivered"/> that the provider
/// confirmed it reached the phone or inbox. Nothing is ever reported as sent on the school's own say-so.
/// </summary>
public enum MessageStatus { Pending, NotSent, Queued, Sent, Delivered, Failed, Cancelled }

/// <summary>
/// One outgoing SMS, WhatsApp message or email (the outbox). Written in the same transaction as the event that
/// caused it, then handed to the channel's provider. <see cref="DedupKey"/> makes "notify once" a database guarantee.
/// </summary>
public sealed class OutboundMessage : TenantEntity
{
    private OutboundMessage() { }

    public OutboundMessage(Guid schoolId, MessageChannel channel, string recipient, string? recipientName, string purpose, string body,
        string dedupKey)
    {
        SchoolId = schoolId;
        Channel = channel;
        Recipient = recipient;
        RecipientName = recipientName;
        Purpose = purpose;
        Body = body;
        DedupKey = dedupKey;
    }

    public MessageChannel Channel { get; private set; }
    public string Recipient { get; private set; } = default!;
    public string? RecipientName { get; private set; }
    /// <summary>What the message is for, e.g. <c>attendance.absent</c>.</summary>
    public string Purpose { get; private set; } = default!;
    public string? Subject { get; set; }
    public string Body { get; private set; } = default!;
    public string DedupKey { get; private set; } = default!;
    public MessageStatus Status { get; private set; } = MessageStatus.Pending;
    public string? StatusDetail { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? StatusChangedAt { get; private set; }
    public Guid? GuardianId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? StaffId { get; set; }
    /// <summary>The record that triggered the message (e.g. the attendance register).</summary>
    public Guid? RelatedId { get; set; }

    public bool IsFinal => Status is MessageStatus.NotSent or MessageStatus.Delivered or MessageStatus.Failed or MessageStatus.Cancelled;

    public void MarkNotSent(string reason, DateTimeOffset at) => Move(MessageStatus.NotSent, reason, at);

    public void MarkAccepted(string provider, string? providerMessageId, DateTimeOffset at)
    {
        Attempts++;
        Provider = provider;
        ProviderMessageId = providerMessageId;
        Move(MessageStatus.Queued, null, at);
    }

    public void MarkFailed(string provider, string reason, DateTimeOffset at)
    {
        Attempts++;
        Provider = provider;
        Move(MessageStatus.Failed, reason, at);
    }

    /// <summary>Applies a provider's status report (sent, delivered or failed).</summary>
    public void ApplyProviderReport(MessageStatus status, string? detail, DateTimeOffset at)
    {
        Guard.Against(status is not (MessageStatus.Sent or MessageStatus.Delivered or MessageStatus.Failed), "message.bad_report",
            "Providers can only report sent, delivered or failed.");
        Guard.Against(Provider is null, "message.no_provider", "No provider has this message.");
        Move(status, detail, at);
    }

    /// <summary>Stops a message that has not left the school (e.g. the absence was corrected before sending).</summary>
    public bool Cancel(string reason, DateTimeOffset at)
    {
        if (Status is not (MessageStatus.Pending or MessageStatus.NotSent))
        {
            return false;
        }

        Move(MessageStatus.Cancelled, reason, at);
        return true;
    }

    /// <summary>Re-arms a cancelled message whose reason came back (an absence corrected away and then recorded again).</summary>
    public bool Reopen(string body, DateTimeOffset at)
    {
        if (Status != MessageStatus.Cancelled)
        {
            return false;
        }

        Body = body;
        Move(MessageStatus.Pending, null, at);
        return true;
    }

    private void Move(MessageStatus to, string? detail, DateTimeOffset at)
    {
        Status = to;
        StatusDetail = detail;
        StatusChangedAt = at;
    }
}
