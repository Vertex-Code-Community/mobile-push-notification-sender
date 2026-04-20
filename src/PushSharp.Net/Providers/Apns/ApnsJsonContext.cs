using System.Text.Json.Serialization;

namespace PushSharp.Net.Providers.Apns;

[JsonSerializable(typeof(ApnsRequest))]
[JsonSerializable(typeof(ApnsErrorResponse))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class ApnsJsonContext : JsonSerializerContext;
