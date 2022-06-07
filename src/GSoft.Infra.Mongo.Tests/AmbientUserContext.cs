namespace GSoft.Infra.Mongo.Tests;

public sealed class AmbientUserContext
{
    private static readonly AsyncLocal<UserContext?> _asyncLocal = new AsyncLocal<UserContext?>();

    public string? UserId => _asyncLocal.Value?.UserId;

    public IDisposable RegisterUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        }

        return new UserContext(userId);
    }

    private class UserContext : IDisposable
    {
        private readonly UserContext? _parent;

        public UserContext(string userId)
        {
            this._parent = _asyncLocal.Value;
            _asyncLocal.Value = this;
            this.UserId = userId;
        }

        public string? UserId { get; }

        public void Dispose()
        {
            _asyncLocal.Value = this._parent;
        }
    }
}