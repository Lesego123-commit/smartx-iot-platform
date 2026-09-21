// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/Dtos.cs
// Purpose: Immutable record DTOs shared by the API and the WPF client.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Records (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

// ---------------------------------------------------------------------------------------
// Data-transfer contracts shared by the API and the WPF client. Kept as immutable
// records in SmartX.Core so both layers compile against a single source of truth.
// ---------------------------------------------------------------------------------------

/// <summary>Request to register a physical sensor / device on the mesh.</summary>
public record RegisterSensorRequest(
    string Mac,
    string Location,
    SensorCategory Category,
    string? Description);

/// <summary>A registered device, as returned by the API.</summary>
public record SensorProfile(
    string Id,
    string Mac,
    string Location,
    SensorCategory Category,
    string? Description,
    DateTimeOffset RegisteredAt,
    string? AttachmentName,
    long AttachmentBytes);

/// <summary>Push one telemetry value. Kind drives which typed field is authoritative.</summary>
public record IngestRequest(
    string DeviceId,
    string Metric,
    TelemetryKind Kind,
    double NumericValue,
    bool BoolValue);

/// <summary>A stored/returned telemetry sample with anomaly tagging for the dashboard.</summary>
public record TelemetrySample(
    string DeviceId,
    string Metric,
    string PayloadType,
    string DisplayValue,
    double? NumericValue,
    DateTimeOffset Timestamp,
    bool IsAnomaly);

/// <summary>A proactive engagement alert surfaced on the live dashboard.</summary>
public record AlertDto(
    string DeviceId,
    AlertSeverity Severity,
    string Message,
    DateTimeOffset Timestamp);

/// <summary>Rolled-up health used by the dashboard's dynamic engagement strip.</summary>
public record IngestionSnapshot(
    int RegisteredDevices,
    int TotalSamples,
    int AnomalyCount,
    int SilentDevices,
    double IngestRatePerMin,
    IReadOnlyList<AlertDto> RecentAlerts,
    IReadOnlyList<DeviceHealth> Devices);

/// <summary>Per-device health tile for the engagement dashboard.</summary>
public record DeviceHealth(
    string DeviceId,
    string Location,
    SensorCategory Category,
    double? LastValue,
    DateTimeOffset? LastSeen,
    int AnomalyCount,
    bool IsSilent);

/// <summary>Request payload for a recursive topology validation.</summary>
public record TopologyValidationRequest(DeploymentNode Root);
