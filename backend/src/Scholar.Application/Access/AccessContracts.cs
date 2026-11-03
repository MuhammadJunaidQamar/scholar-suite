using FluentValidation;
using Scholar.Application.Common;
using Scholar.Domain.Access;
using Scholar.Domain.Identity;

namespace Scholar.Application.Access;

public sealed record RoleListItemDto(Guid Id, string? Key, string Name, string? Description, Portal Portal, bool IsSystem,
    bool IsLocked, int PermissionCount, int MemberCount);

public sealed record RoleDetailDto(Guid Id, string? Key, string Name, string? Description, Portal Portal, bool IsSystem,
    bool IsLocked, IReadOnlyList<string> Permissions);

public sealed record SaveRoleRequest(string Name, string? Description, Portal Portal, IReadOnlyList<string> Permissions);

public sealed record RoleAssignmentInput(Guid RoleId, Guid? CampusId);

public sealed record RoleAssignmentDto(Guid RoleId, string RoleName, string? RoleKey, Guid? CampusId, string? CampusName);

public sealed record MemberListItemDto(Guid UserId, string FullName, string? Email, string UserName, UserStatus Status,
    MembershipStatus MembershipStatus, DateTimeOffset? LastLoginAt, IReadOnlyList<RoleAssignmentDto> Roles);

public sealed record InviteMemberRequest(string FirstName, string LastName, string Email, IReadOnlyList<RoleAssignmentInput> Roles);

/// <param name="Handover">How the new account's first sign-in was handed over; null when they already had an account.</param>
public sealed record InviteMemberResult(MemberListItemDto Member, Identity.AccountHandover? Handover);

public sealed record UpdateMemberRolesRequest(IReadOnlyList<RoleAssignmentInput> Roles);

public sealed record MemberQuery : PageQuery
{
    public Guid? RoleId { get; init; }
    public MembershipStatus? Status { get; init; }
}

public sealed record AccessDto(IReadOnlyList<string> Permissions, IReadOnlyList<Portal> Portals, IReadOnlyList<string> Roles,
    IReadOnlyList<Guid>? CampusIds);

public sealed record SchoolContextDto(Guid Id, string Code, string Name, string? ShortName, Guid? LogoFileId, string? BrandColor,
    string Timezone, string CurrencyCode, string Locale, IReadOnlyList<string> EnabledModules);

public sealed record MeDto(Identity.AuthUserDto User, SchoolContextDto? School, IReadOnlyList<Identity.SchoolSummaryDto> Schools,
    AccessDto? Access);

public sealed class SaveRoleRequestValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Permissions).NotNull();
        RuleForEach(x => x.Permissions).Must(PermissionCatalog.Exists).WithMessage("Unknown permission '{PropertyValue}'.");
    }
}

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("Assign at least one role.");
    }
}

public sealed class UpdateMemberRolesRequestValidator : AbstractValidator<UpdateMemberRolesRequest>
{
    public UpdateMemberRolesRequestValidator() => RuleFor(x => x.Roles).NotNull();
}
