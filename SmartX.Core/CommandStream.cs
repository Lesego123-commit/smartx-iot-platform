// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
// File:    SmartX.Core/CommandStream.cs
// Purpose: Shared enums and DTO contracts for the Real-Time Command Stream (commands,
//          device state, stream status, timeline points and predictive insights).
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Records (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record [Accessed 25 September 2026].
//   - Microsoft, 2024. Enumeration types (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>A manual override command an operator can issue to a device.</summary>
public enum CommandAction
{
    TriggerPump,
    StopPump,
    ToggleRelay,
    OpenValve,
    ShutValve,
    ResetNode,
    AcknowledgeAlert
}

/// <summary>Operational health state of a live device on the command stream.</summary>
public enum DeviceAlertState
{
    Normal,
    Warning,
    Critical,
    Disconnected
}

/// <summary>Request body to issue a manual override command to a device.</summary>
public record DeviceCommand(string DeviceId, CommandAction Action);

/// <summary>An executed command, stored on the undo stack and shown in history.</summary>
public record CommandRecord(
    string Id,
    string DeviceId,
    CommandAction Action,
    DateTimeOffset IssuedAt,
    string Summary,
    bool IsUndone);

/// <summary>Live operational snapshot of one device (from the device dictionary).</summary>
public record DeviceStateDto(
    string DeviceId,
    string Location,
    SensorCategory Category,
    string PrimaryMetric,
    double? LastValue,
    DateTimeOffset? LastSeen,
    DeviceAlertState AlertState,
    bool PumpOn,
    bool RelayOn);

/// <summary>Depths and counters exposing how the backend queues are processing volatile data.</summary>
public record StreamStatus(
    int StandardQueueDepth,
    int PriorityQueueDepth,
    long StandardProcessed,
    long PriorityProcessed,
    int ActiveErrorCount,
    int CommandHistoryDepth,
    int RegisteredDevices);

/// <summary>One timestamp-ordered reading, read back from the sorted timeline.</summary>
public record TimelinePoint(DateTimeOffset Timestamp, double Value);

/// <summary>A proactive recommendation produced by the predictive action engine.</summary>
public record Insight(
    string Title,
    string Detail,
    string? DeviceId,
    CommandAction? SuggestedAction,
    AlertSeverity Severity);
