namespace Workleap.Extensions.Mongo.Tests;

public sealed class AmbientUserContext
{
    private static readonly AsyncLocal<UserContext?> AsyncLocal = new AsyncLocal<UserContext?>();

    public string? UserId => AsyncLocal.Value?.UserId;

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
            this._parent = AsyncLocal.Value;
            AsyncLocal.Value = this;
            this.UserId = userId;
        }

        public string? UserId { get; }

        public void Dispose()
        {
            AsyncLocal.Value = this._parent;
        }
    }
}