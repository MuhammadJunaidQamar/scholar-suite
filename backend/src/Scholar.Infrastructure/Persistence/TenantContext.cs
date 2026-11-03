using Scholar.Application.Abstractions;

namespace Scholar.Infrastructure.Persistence;

internal sealed class TenantContext(ICurrentUser currentUser) : ITenantContext
{
    private readonly Stack<Guid> _overrides = new();

    public Guid? SchoolId => _overrides.Count > 0 ? _overrides.Peek() : currentUser.SchoolId;

    public IDisposable Enter(Guid schoolId)
    {
        _overrides.Push(schoolId);
        return new Scope(this);
    }

    private sealed class Scope(TenantContext owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner._overrides.Pop();
        }
    }
}
