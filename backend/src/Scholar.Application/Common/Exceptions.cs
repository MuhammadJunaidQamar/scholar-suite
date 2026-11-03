namespace Scholar.Application.Common;

/// <summary>The requested resource does not exist in the caller's tenant/scope. Surfaces as HTTP 404.</summary>
public sealed class NotFoundException(string resource, object? key = null)
    : Exception(key is null ? $"{resource} was not found." : $"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

/// <summary>The caller is authenticated but not allowed to perform the operation. Surfaces as HTTP 403.</summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : Exception(message);

/// <summary>The request conflicts with current state (duplicate, stale version…). Surfaces as HTTP 409.</summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
