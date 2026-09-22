// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/TelemetryPacket.cs
// Purpose: Generic, boxing-free telemetry wrapper TelemetryPacket<T>.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Generic classes and methods (C# Programming Guide). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/ [Accessed 10 September 2026].
//   - Microsoft, 2024. Constraints on type parameters (C# Programming Guide). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/generics/constraints-on-type-parameters [Accessed 10 September 2026].
//   - Microsoft, 2023. Boxing and unboxing (C# Programming Guide). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Globalization;

namespace SmartX.Core;

/// <summary>
/// Non-generic façade so heterogeneous packets (float, int, bool ...) can live in a
/// single collection and be rendered/serialised uniformly, while the concrete
/// <see cref="TelemetryPacket{T}"/> keeps its payload strongly typed underneath.
/// </summary>
public interface ITelemetryPacket
{
    string DeviceId { get; }
    string Metric { get; }
    DateTimeOffset Timestamp { get; }
    /// <summary>CLR type name of the payload, e.g. "Single", "Int32", "Boolean".</summary>
    string PayloadType { get; }
    /// <summary>Display value produced without boxing the payload (see remarks in the class).</summary>
    string ValueAsString();
    /// <summary>Numeric projection for charting; bools map to 0/1. Null when not numeric.</summary>
    double? AsNumeric();
}

/// <summary>
/// Reusable generic wrapper that carries one telemetry value of any value type
/// (<c>float</c> for temperature, <c>int</c> for power metrics, <c>bool</c> for smart
/// switch triggers) uniformly.
///
/// The payload is stored as <typeparamref name="T"/> — never as <see cref="object"/> —
/// so building and reading a packet on the high-throughput ingestion path performs
/// <b>no boxing/unboxing</b>. The <c>where T : struct</c> constraint keeps the type a
/// value type, and calls such as <c>Value.ToString()</c> compile to a
/// <c>constrained.callvirt</c> that dispatches directly on the struct without boxing.
/// </summary>
public sealed class TelemetryPacket<T> : ITelemetryPacket where T : struct
{
    public string DeviceId { get; }
    public string Metric { get; }
    public DateTimeOffset Timestamp { get; }

    /// <summary>The strongly-typed payload. No boxing occurs when it is read in generic code.</summary>
    public T Value { get; }

    public string PayloadType => typeof(T).Name;

    public TelemetryPacket(string deviceId, string metric, T value, DateTimeOffset? timestamp = null)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Metric = metric ?? throw new ArgumentNullException(nameof(metric));
        Value = value;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Type-safe projection to another value type — stays boxing-free because the
    /// mapping runs against <typeparamref name="T"/> directly.
    /// </summary>
    public TelemetryPacket<TOut> Select<TOut>(Func<T, TOut> map) where TOut : struct
        => new(DeviceId, Metric, map(Value), Timestamp);

    // constrained.callvirt on a value type overriding ToString() -> no boxing.
    public string ValueAsString() => Value switch
    {
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        bool b => b ? "ON" : "OFF",
        _ => Value.ToString() ?? string.Empty
    };

    public double? AsNumeric() => Value switch
    {
        float f => f,
        double d => d,
        int i => i,
        long l => l,
        bool b => b ? 1d : 0d,
        _ => null
    };

    public override string ToString() => $"{DeviceId}/{Metric} = {ValueAsString()} ({PayloadType})";
}

/// <summary>
/// Convenience factory so the caller gets type inference:
/// <c>TelemetryPacket.Create("esp32-01", "soil_moisture", 42.7f)</c>.
/// </summary>
public static class TelemetryPacket
{
    public static TelemetryPacket<T> Create<T>(string deviceId, string metric, T value,
        DateTimeOffset? timestamp = null) where T : struct
        => new(deviceId, metric, value, timestamp);
}
