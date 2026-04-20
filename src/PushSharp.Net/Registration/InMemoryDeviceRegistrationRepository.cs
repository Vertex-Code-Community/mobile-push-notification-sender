namespace PushSharp.Net.Registration;

/// <summary>
/// Non-thread-safe in-memory <see cref="IDeviceRegistrationRepository"/> for samples and tests.
/// </summary>
public sealed class InMemoryDeviceRegistrationRepository : IDeviceRegistrationRepository
{
    private readonly List<DeviceRegistration> _items = [];

    public Task SaveAsync(DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(r => r.DeviceId == registration.DeviceId);
        _items.Add(registration);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(r => r.DeviceId == deviceId);
        return Task.CompletedTask;
    }

    public Task RemoveByTokenAsync(string deviceToken, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(r => r.DeviceToken == deviceToken);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetTokensByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_items.Where(r => r.UserId == userId).Select(r => r.DeviceToken).ToList());

    public Task<IReadOnlyList<string>> GetTokensByTagAsync(string tag, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_items.Where(r => r.Tags.Contains(tag)).Select(r => r.DeviceToken).ToList());

    public Task<IReadOnlyList<string>> GetAllTokensAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(_items.Select(r => r.DeviceToken).ToList());
}
