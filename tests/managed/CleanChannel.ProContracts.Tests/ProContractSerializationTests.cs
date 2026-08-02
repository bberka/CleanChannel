using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CleanChannel.ProContracts;
using Xunit;

namespace CleanChannel.ProContracts.Tests;

public sealed class ProContractSerializationTests
{
    [Fact]
    public void CapabilityIdentifiersUseStableStringValues()
    {
        var json = JsonSerializer.Serialize(
            ProCapabilityIds.AiNoiseCancellation,
            ProJsonContext.Default.String);

        Assert.Equal("\"ai-noise-cancellation\"", json);
    }

    [Fact]
    public void HostStatusRoundTripsThroughGeneratedJsonMetadata()
    {
        var status = new ProHostStatusDto(
            ObservedAt: new DateTimeOffset(2026, 8, 2, 12, 30, 0, TimeSpan.Zero),
            Connection: ProHostConnectionState.Connected,
            Readiness: ProHostReadiness.Ready,
            HostVersion: "0.1.0",
            Entitlement: new ProEntitlementStateDto(
                ProEntitlementKind.Annual,
                new DateTimeOffset(2027, 8, 2, 0, 0, 0, TimeSpan.Zero),
                [ProCapabilityIds.AiNoiseCancellation, ProCapabilityIds.Sync]),
            Features:
            [
                new ProFeatureStatusDto(
                    ProCapabilityIds.AiNoiseCancellation,
                    ProFeatureAvailability.Available,
                    null),
                new ProFeatureStatusDto(
                    ProCapabilityIds.PremiumDsp,
                    ProFeatureAvailability.Degraded,
                    "DSP_BACKEND_LIMITED"),
            ],
            Error: null);

        var json = JsonSerializer.Serialize(status, ProJsonContext.Default.ProHostStatusDto);
        var roundTripped = JsonSerializer.Deserialize(json, ProJsonContext.Default.ProHostStatusDto)
            ?? throw new InvalidOperationException("Generated metadata returned no host status.");

        Assert.Contains("\"connection\":\"Connected\"", json);
        Assert.Contains("\"readiness\":\"Ready\"", json);
        Assert.Contains("\"kind\":\"Annual\"", json);
        Assert.Equal(status.ObservedAt, roundTripped.ObservedAt);
        Assert.Equal(status.Connection, roundTripped.Connection);
        Assert.Equal(status.Readiness, roundTripped.Readiness);
        Assert.Equal(status.HostVersion, roundTripped.HostVersion);
        Assert.Equal(status.Entitlement.Kind, roundTripped.Entitlement.Kind);
        Assert.Equal(status.Entitlement.ExpiresAt, roundTripped.Entitlement.ExpiresAt);
        Assert.Equal(status.Entitlement.FeatureIds, roundTripped.Entitlement.FeatureIds);
        Assert.Equal(status.Features, roundTripped.Features);
        Assert.Null(roundTripped.Error);
    }

    [Fact]
    public void UnavailableProStateIsRepresentableWithoutAProHost()
    {
        var unavailable = new ProHostStatusDto(
            ObservedAt: new DateTimeOffset(2026, 8, 2, 12, 30, 0, TimeSpan.Zero),
            Connection: ProHostConnectionState.Disconnected,
            Readiness: ProHostReadiness.Unavailable,
            HostVersion: null,
            Entitlement: new ProEntitlementStateDto(
                ProEntitlementKind.None,
                ExpiresAt: null,
                FeatureIds: []),
            Features:
            [
                new ProFeatureStatusDto(
                    ProCapabilityIds.AiNoiseCancellation,
                    ProFeatureAvailability.Unavailable,
                    "PRO_HOST_NOT_CONNECTED"),
            ],
            Error: new ProIpcErrorDto(
                "PRO_HOST_NOT_CONNECTED",
                "The optional Pro host is not connected."));

        var json = JsonSerializer.Serialize(
            unavailable,
            ProJsonContext.Default.ProHostStatusDto);
        var roundTripped = JsonSerializer.Deserialize(
            json,
            ProJsonContext.Default.ProHostStatusDto)
            ?? throw new InvalidOperationException("Generated metadata returned no host status.");

        Assert.Equal(ProHostReadiness.Unavailable, roundTripped.Readiness);
        Assert.Equal(ProEntitlementKind.None, roundTripped.Entitlement.Kind);
        Assert.Equal(ProFeatureAvailability.Unavailable, roundTripped.Features[0].Availability);
        Assert.Equal("PRO_HOST_NOT_CONNECTED", roundTripped.Error?.Code);
    }
}

public sealed class ProHandshakeContractTests
{
    [Fact]
    public void HandshakeRoundTripsWithProtocolVersionAndMachineReadableError()
    {
        var response = new ProHostHandshakeResponse(
            ProtocolVersion: ProProtocol.CurrentVersion,
            Connection: ProHostConnectionState.Faulted,
            Readiness: ProHostReadiness.Failed,
            HostVersion: null,
            SupportedCapabilities: [],
            Attestation: new ProHostAttestationDto(
                HostId: "cleanchannel-pro-host",
                PublisherThumbprint: "publisher-thumbprint",
                SignatureAlgorithm: "ECDSA-SHA256",
                ChallengeSignature: "signature"),
            Error: new ProIpcErrorDto("PROTOCOL_MISMATCH", "The requested protocol is unsupported."));

        var json = JsonSerializer.Serialize(
            response,
            ProJsonContext.Default.ProHostHandshakeResponse);
        var roundTripped = JsonSerializer.Deserialize(
            json,
            ProJsonContext.Default.ProHostHandshakeResponse)
            ?? throw new InvalidOperationException("Generated metadata returned no handshake response.");

        Assert.Contains("\"protocolVersion\":1", json);
        Assert.Contains("\"code\":\"PROTOCOL_MISMATCH\"", json);
        Assert.Equal(response.ProtocolVersion, roundTripped.ProtocolVersion);
        Assert.Equal(response.Connection, roundTripped.Connection);
        Assert.Equal(response.Readiness, roundTripped.Readiness);
        Assert.Equal(response.HostVersion, roundTripped.HostVersion);
        Assert.Equal(response.SupportedCapabilities, roundTripped.SupportedCapabilities);
        Assert.Equal(response.Attestation, roundTripped.Attestation);
        Assert.Equal(response.Error, roundTripped.Error);
    }

    [Fact]
    public void UnknownCapabilityIdentifiersRemainForwardCompatible()
    {
        var response = new ProHostHandshakeResponse(
            ProProtocol.CurrentVersion,
            ProHostConnectionState.Connected,
            ProHostReadiness.Ready,
            "0.1.0",
            [ProCapabilityIds.AiNoiseCancellation, "future-capability"],
            Attestation: null,
            Error: null);

        var json = JsonSerializer.Serialize(response, ProJsonContext.Default.ProHostHandshakeResponse);
        var roundTripped = JsonSerializer.Deserialize(json, ProJsonContext.Default.ProHostHandshakeResponse)
            ?? throw new InvalidOperationException("Generated metadata returned no handshake response.");

        Assert.Equal("future-capability", roundTripped.SupportedCapabilities[1]);
    }

    [Fact]
    public void UnknownWireEnumValuesMapToUnknown()
    {
        const string json = "{\"protocolVersion\":1,\"connection\":\"FutureConnection\",\"readiness\":\"FutureReadiness\",\"hostVersion\":null,\"supportedCapabilities\":[],\"attestation\":null,\"error\":null}";

        var response = JsonSerializer.Deserialize(json, ProJsonContext.Default.ProHostHandshakeResponse)
            ?? throw new InvalidOperationException("Generated metadata returned no handshake response.");

        Assert.Equal(ProHostConnectionState.Unknown, response.Connection);
        Assert.Equal(ProHostReadiness.Unknown, response.Readiness);
    }

    [Fact]
    public void SignedChallengeAttestationIsValidatedAgainstPinnedCertificate()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var certificateRequest = new CertificateRequest(
            "CN=CleanChannel Test Publisher",
            signingKey,
            HashAlgorithmName.SHA256);
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5));

        var request = new ProHostHandshakeRequest(
            ProProtocol.CurrentVersion,
            "0.1.0",
            "test-challenge-0123456789");
        var signature = signingKey.SignData(
            Encoding.UTF8.GetBytes(request.Challenge),
            HashAlgorithmName.SHA256);
        var response = new ProHostHandshakeResponse(
            ProProtocol.CurrentVersion,
            ProHostConnectionState.Connected,
            ProHostReadiness.Ready,
            "0.1.0",
            [],
            new ProHostAttestationDto(
                "test-host",
                certificate.Thumbprint,
                "ECDSA-SHA256",
                Convert.ToBase64String(signature)),
            null);

        var valid = ProHostAttestationValidator.TryValidate(
            request,
            response,
            certificate,
            out var errorCode);

        Assert.True(valid);
        Assert.Empty(errorCode);
    }
}
