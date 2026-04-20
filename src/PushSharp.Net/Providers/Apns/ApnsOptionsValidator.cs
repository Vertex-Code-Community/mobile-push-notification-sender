using Microsoft.Extensions.Options;

namespace PushSharp.Net.Providers.Apns;

/// <summary>
/// Validates mutual-exclusion constraints on <see cref="ApnsOptions"/> at startup.
/// Exactly one of PrivateKeyFilePath or PrivateKeyContent must be set.
/// Registered in DI by <c>AddPushNotifications()</c>; fires via <c>ValidateOnStart()</c>.
/// </summary>
internal sealed class ApnsOptionsValidator : IValidateOptions<ApnsOptions>
{
    public ValidateOptionsResult Validate(string? name, ApnsOptions options)
    {
        var failures = new List<string>();

        bool hasFile = !string.IsNullOrWhiteSpace(options.PrivateKeyFilePath);
        bool hasContent = !string.IsNullOrWhiteSpace(options.PrivateKeyContent);

        if (!hasFile && !hasContent)
        {
            failures.Add(
                "Either ApnsOptions.PrivateKeyFilePath or ApnsOptions.PrivateKeyContent must be set.");
        }

        if (hasFile && hasContent)
        {
            failures.Add(
                "ApnsOptions.PrivateKeyFilePath and ApnsOptions.PrivateKeyContent are mutually exclusive. " +
                "Set only one.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
