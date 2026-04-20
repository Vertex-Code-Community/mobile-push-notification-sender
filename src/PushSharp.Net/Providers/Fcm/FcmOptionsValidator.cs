using Microsoft.Extensions.Options;

namespace PushSharp.Net.Providers.Fcm;

/// <summary>
/// Validates mutual-exclusion constraints on <see cref="FcmOptions"/> at startup.
/// Exactly one of CredentialFilePath or CredentialJson must be set.
/// Registered in DI by <c>AddPushNotifications()</c>; fires via <c>ValidateOnStart()</c>.
/// </summary>
internal sealed class FcmOptionsValidator : IValidateOptions<FcmOptions>
{
    public ValidateOptionsResult Validate(string? name, FcmOptions options)
    {
        var failures = new List<string>();

        bool hasFile = !string.IsNullOrWhiteSpace(options.CredentialFilePath);
        bool hasJson = !string.IsNullOrWhiteSpace(options.CredentialJson);

        if (!hasFile && !hasJson)
        {
            failures.Add(
                "Either FcmOptions.CredentialFilePath or FcmOptions.CredentialJson must be set.");
        }

        if (hasFile && hasJson)
        {
            failures.Add(
                "FcmOptions.CredentialFilePath and FcmOptions.CredentialJson are mutually exclusive. " +
                "Set only one.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
