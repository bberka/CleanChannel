using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CleanChannel.ProContracts;

public static class ProProtocol
{
    public const int CurrentVersion = 1;
}

public static class ProCapabilityIds
{
    public const string AiNoiseCancellation = "ai-noise-cancellation";
    public const string PremiumDsp = "premium-dsp";
    public const string PremiumPresets = "premium-presets";
    public const string Sync = "sync";
}

[JsonConverter(typeof(ProHostConnectionStateJsonConverter))]
public enum ProHostConnectionState
{
    Unknown,
    Disconnected,
    Connecting,
    Connected,
    Faulted,
}

[JsonConverter(typeof(ProHostReadinessJsonConverter))]
public enum ProHostReadiness
{
    Unknown,
    Unavailable,
    Starting,
    Ready,
    Degraded,
    Failed,
}

[JsonConverter(typeof(ProEntitlementKindJsonConverter))]
public enum ProEntitlementKind
{
    Unknown,
    None,
    Annual,
    Lifetime,
}

[JsonConverter(typeof(ProFeatureAvailabilityJsonConverter))]
public enum ProFeatureAvailability
{
    Unknown,
    Unavailable,
    Available,
    Degraded,
}

public sealed record ProEntitlementStateDto(
    ProEntitlementKind Kind,
    DateTimeOffset? ExpiresAt,
    IReadOnlyList<string> FeatureIds);

public sealed record ProFeatureStatusDto(
    string FeatureId,
    ProFeatureAvailability Availability,
    string? ReasonCode);

public sealed record ProIpcErrorDto(
    string Code,
    string Message);

public sealed record ProHostHandshakeRequest(
    int ProtocolVersion,
    string ClientVersion,
    string Challenge);

public sealed record ProHostAttestationDto(
    string HostId,
    string PublisherThumbprint,
    string SignatureAlgorithm,
    string ChallengeSignature);

public sealed record ProHostHandshakeResponse(
    int ProtocolVersion,
    ProHostConnectionState Connection,
    ProHostReadiness Readiness,
    string? HostVersion,
    IReadOnlyList<string> SupportedCapabilities,
    ProHostAttestationDto? Attestation,
    ProIpcErrorDto? Error);

public sealed record ProHostStatusDto(
    DateTimeOffset ObservedAt,
    ProHostConnectionState Connection,
    ProHostReadiness Readiness,
    string? HostVersion,
    ProEntitlementStateDto Entitlement,
    IReadOnlyList<ProFeatureStatusDto> Features,
    ProIpcErrorDto? Error);

public sealed record GetProHostStatusRequest;

public sealed record GetProHostStatusResponse(ProHostStatusDto Status);

public static class ProHostAttestationValidator
{
    public static bool TryValidate(
        ProHostHandshakeRequest request,
        ProHostHandshakeResponse response,
        X509Certificate2 trustedPublisherCertificate,
        out string errorCode)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(trustedPublisherCertificate);

        if (request.ProtocolVersion != response.ProtocolVersion)
        {
            errorCode = "PROTOCOL_MISMATCH";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Challenge)
            || request.Challenge.Length > 1024
            || response.Attestation is not { } attestation
            || string.IsNullOrWhiteSpace(attestation.HostId))
        {
            errorCode = "PRO_HANDSHAKE_INVALID";
            return false;
        }

        if (!string.Equals(
                NormalizeThumbprint(attestation.PublisherThumbprint),
                NormalizeThumbprint(trustedPublisherCertificate.Thumbprint),
                StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "PRO_PUBLISHER_MISMATCH";
            return false;
        }

        if (!string.Equals(attestation.SignatureAlgorithm, "ECDSA-SHA256", StringComparison.Ordinal))
        {
            errorCode = "PRO_SIGNATURE_ALGORITHM_UNSUPPORTED";
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(attestation.ChallengeSignature);
        }
        catch (FormatException)
        {
            errorCode = "PRO_SIGNATURE_INVALID";
            return false;
        }

        using var ecdsa = trustedPublisherCertificate.GetECDsaPublicKey();
        if (ecdsa is null
            || !ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(request.Challenge),
                signature,
                HashAlgorithmName.SHA256))
        {
            errorCode = "PRO_SIGNATURE_INVALID";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static string NormalizeThumbprint(string? thumbprint)
    {
        return string.IsNullOrWhiteSpace(thumbprint)
            ? string.Empty
            : thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}

public abstract class TolerantStringEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string for {typeof(TEnum).Name}.");
        }

        var value = reader.GetString();
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : Enum.Parse<TEnum>("Unknown");
    }

    public override void Write(
        Utf8JsonWriter writer,
        TEnum value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public sealed class ProHostConnectionStateJsonConverter
    : TolerantStringEnumJsonConverter<ProHostConnectionState>;

public sealed class ProHostReadinessJsonConverter
    : TolerantStringEnumJsonConverter<ProHostReadiness>;

public sealed class ProEntitlementKindJsonConverter
    : TolerantStringEnumJsonConverter<ProEntitlementKind>;

public sealed class ProFeatureAvailabilityJsonConverter
    : TolerantStringEnumJsonConverter<ProFeatureAvailability>;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(ProHostConnectionState))]
[JsonSerializable(typeof(ProHostReadiness))]
[JsonSerializable(typeof(ProEntitlementKind))]
[JsonSerializable(typeof(ProFeatureAvailability))]
[JsonSerializable(typeof(ProEntitlementStateDto))]
[JsonSerializable(typeof(ProFeatureStatusDto))]
[JsonSerializable(typeof(ProIpcErrorDto))]
[JsonSerializable(typeof(ProHostHandshakeRequest))]
[JsonSerializable(typeof(ProHostAttestationDto))]
[JsonSerializable(typeof(ProHostHandshakeResponse))]
[JsonSerializable(typeof(ProHostStatusDto))]
[JsonSerializable(typeof(GetProHostStatusRequest))]
[JsonSerializable(typeof(GetProHostStatusResponse))]
public partial class ProJsonContext : JsonSerializerContext;
