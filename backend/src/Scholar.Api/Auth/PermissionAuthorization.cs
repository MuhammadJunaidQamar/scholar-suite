using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Scholar.Api.Infrastructure;
using Scholar.Application.Abstractions;
using Scholar.Application.Access;
using Scholar.Application.Common;
using Scholar.Application.Identity;

namespace Scholar.Api.Auth;

/// <summary>Satisfied when the user holds any one of <see cref="Permissions"/> in the active school.</summary>
internal sealed class PermissionRequirement(IReadOnlyList<string> permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

internal sealed class PermissionHandler(IAccessContext access) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        try
        {
            var effective = await access.GetAsync();
            if (requirement.Permissions.Any(effective.Has))
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception ex) when (ex is ForbiddenException or UnauthorizedException)
        {
            // Not authorized: leave the requirement unsatisfied.
        }
    }
}

/// <summary>Builds <c>perm:a|b</c> policies on demand so endpoints can name permissions directly.</summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";
    public const string PlatformPolicy = "platform";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..].Split('|')))
                .Build();
        }

        if (policyName == PlatformPolicy)
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(ScholarClaims.PlatformAdmin, "1")
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>Returns 404 for endpoints of optional modules the school has switched off.</summary>
internal sealed class ModuleGateFilter(string module) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var gate = context.HttpContext.RequestServices.GetRequiredService<ISchoolModuleGate>();
        if (!await gate.IsEnabledAsync(module, context.HttpContext.RequestAborted))
        {
            return Results.Problem(statusCode: 404, title: "Module not enabled",
                detail: "This feature is not enabled for your school.", extensions: new Dictionary<string, object?> { ["code"] = "module.disabled" });
        }

        return await next(context);
    }
}

internal static class AuthorizationConventions
{
    /// <summary>Requires any one of the given permissions.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, params string[] permissions)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.Prefix + string.Join('|', permissions));

    public static TBuilder RequirePlatformAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PermissionPolicyProvider.PlatformPolicy);

    public static RouteGroupBuilder RequireModule(this RouteGroupBuilder group, string module)
    {
        group.AddEndpointFilter(new ModuleGateFilter(module));
        return group;
    }

    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        return services;
    }
}
