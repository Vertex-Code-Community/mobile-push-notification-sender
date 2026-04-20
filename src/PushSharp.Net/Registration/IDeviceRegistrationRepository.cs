namespace PushSharp.Net.Registration;

/// <summary>
/// Persistence contract for device registrations.
/// <para>
/// PushSharp.Net defines this interface; the consuming application implements it
/// using its own database (EF Core, Dapper, Redis, etc.) and registers via
/// <c>builder.AddDeviceStore&lt;T&gt;()</c>.
/// </para>
/// <para>
/// This is the same pattern as <c>IUserStore&lt;T&gt;</c> in ASP.NET Identity —
/// the library owns the contract, the app owns the storage.
/// </para>
/// </summary>
public interface IDeviceRegistrationRepository
{
    /// <summary>Creates or updates a device registration.</summary>
    Task SaveAsync(DeviceRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>Removes a device registration by its device ID.</summary>
    Task RemoveAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>Removes a registration by its push token (called when a dead token is detected).</summary>
    Task RemoveByTokenAsync(string deviceToken, CancellationToken cancellationToken = default);

    /// <summary>Returns all push tokens registered to a specific user.</summary>
    Task<IReadOnlyList<string>> GetTokensByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Returns all push tokens that have a specific tag.</summary>
    Task<IReadOnlyList<string>> GetTokensByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>Returns all registered push tokens (use with caution for large audiences).</summary>
    Task<IReadOnlyList<string>> GetAllTokensAsync(CancellationToken cancellationToken = default);
}
