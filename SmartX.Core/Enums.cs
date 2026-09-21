// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/Enums.cs
// Purpose: Domain enumerations (sensor category, telemetry kind, alert severity).
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Enumeration types (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>
/// High-level classification of a physical device registered on the Smart-X mesh.
/// Chosen from the assessment brief (Environmental, Power Consumption, Actuator).
/// </summary>
public enum SensorCategory
{
    Environmental,      // soil moisture, temperature, humidity ...
    PowerConsumption,   // smart-meter wattage / voltage ...
    Actuator            // valves, relays, smart switches ...
}

/// <summary>
/// The primitive payload a microcontroller can publish. Used to drive the
/// generic <see cref="TelemetryPacket{T}"/> without boxing the underlying value.
/// </summary>
public enum TelemetryKind
{
    Float,   // e.g. soil moisture   -> float
    Int,     // e.g. power wattage   -> int
    Bool     // e.g. valve state     -> bool
}

/// <summary>Severity band for a proactive engagement alert raised on the dashboard.</summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}
