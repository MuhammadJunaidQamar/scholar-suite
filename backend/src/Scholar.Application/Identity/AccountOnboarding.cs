using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Scholar.Domain.Identity;

namespace Scholar.Application.Identity;

/// <summary>What happened to a message handed to the delivery provider — never more than the provider confirmed.</summary>
public enum DeliveryState
{
    /// <summary>Nothing was sent (no provider configured, or the provider refused it).</summary>
    NotSent,

    /// <summary>Accepted into the outbox; delivery will be attempted and tracked.</summary>
    Queued,

    /// <summary>The provider accepted the message. This is not proof that it reached the inbox.</summary>
    Sent,
}

public sealed record DeliveryResult(DeliveryState State, string? Detail = null);

/// <summary>
/// How a new account's first sign-in was handed over, reported truthfully to whoever created it: an invitation the
/// provider accepted or queued, or — when nothing could be sent — a one-time temporary password to pass on in person.
/// </summary>
/// <param name="Invitation">What happened to the invitation e-mail.</param>
/// <param name="SentTo">The address the invitation went to, when it was sent or queued.</param>
/// <param name="TemporaryPassword">Shown once; the person must choose their own password at first sign-in.</param>
public sealed record AccountHandover(DeliveryState Invitation, string? SentTo, string? TemporaryPassword, string? Detail);

/// <summary>Gives a new account (staff, guardian, school owner) its first way in.</summary>
public sealed class AccountOnboarding(UserManager<User> users, IAccountNotifier notifier, IOptions<AuthOptions> authOptions)
{
    /// <summary>Only for accounts that have no password yet (new, or invited but never signed in).</summary>
    public async Task<AccountHandover> HandOverAsync(User user, string schoolName, CancellationToken ct)
    {
        if (await users.HasPasswordAsync(user))
        {
            throw new InvalidOperationException("This account already has a password; it needs no handover.");
        }

        var reason = "There is no email address on record, so no invitation could be sent.";
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = $"{authOptions.Value.PublicAppUrl.TrimEnd('/')}/accept-invite?uid={user.Id}&token={Uri.EscapeDataString(token)}";
            var delivery = await notifier.SendInvitationAsync(user, schoolName, link, ct);
            if (delivery.State != DeliveryState.NotSent)
            {
                return new AccountHandover(delivery.State, user.Email, null, delivery.Detail);
            }

            reason = delivery.Detail ?? "The invitation could not be sent.";
        }

        // Nothing reached the person: issue a one-time password for the office to pass on. It must be changed at
        // first sign-in, and setting it invalidates any invitation link generated above.
        return new AccountHandover(DeliveryState.NotSent, null, await SetTemporaryPasswordAsync(user), reason);
    }

    /// <summary>
    /// Replaces the account's password with a one-time password that must be changed at the next sign-in, and lifts
    /// any lockout. Callers end the person's sessions and decide who may do this.
    /// </summary>
    public async Task<AccountHandover> ResetToTemporaryPasswordAsync(User user, CancellationToken ct)
    {
        if (await users.HasPasswordAsync(user))
        {
            var removed = await users.RemovePasswordAsync(user);
            if (!removed.Succeeded) throw new DomainExceptionFromIdentity(removed);
        }

        var password = await SetTemporaryPasswordAsync(user);
        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        return new AccountHandover(DeliveryState.NotSent, null, password, "A school administrator issued a new one-time password.");
    }

    /// <summary>Whether account e-mails can be delivered at all (system-wide; says nothing about any account).</summary>
    public bool CanEmail => notifier.CanDeliver;

    private async Task<string> SetTemporaryPasswordAsync(User user)
    {
        var password = TemporaryPassword.Create();
        var result = await users.AddPasswordAsync(user, password);
        if (!result.Succeeded)
        {
            throw new DomainExceptionFromIdentity(result);
        }

        user.MustChangePassword = true;
        await users.UpdateAsync(user);
        return password;
    }
}

/// <summary>Readable one-time passwords (e.g. "Kestrel-4821") that satisfy the password policy.</summary>
public static class TemporaryPassword
{
    private const string Letters = "abcdefghjkmnpqrstuvwxyz";

    public static string Create()
    {
        var word = new string(Enumerable.Range(0, 7).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
        return $"{char.ToUpperInvariant(word[0])}{word[1..]}-{RandomNumberGenerator.GetInt32(1000, 10000)}";
    }
}
