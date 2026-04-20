using System.Text.Json.Serialization;

namespace PushSharp.Net.Providers.Fcm;

[JsonSerializable(typeof(FcmRequest))]
[JsonSerializable(typeof(FcmErrorResponse))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class FcmJsonContext : JsonSerializerContext;
