// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/SensorReading.cs
// Purpose: Value-type reading with overloaded arithmetic and comparison operators.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Operator overloading (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/operator-overloading [Accessed 10 September 2026].
//   - Microsoft, 2024. Structure types (C# reference). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct [Accessed 10 September 2026].
//   - Microsoft, 2024. How to define value equality for a type (C# Programming Guide). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/how-to-define-value-equality-for-a-type [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Globalization;

namespace SmartX.Core;

/// <summary>
/// A single scalar reading from a smart meter / environmental probe.
///
/// Operator overloading lets the engine aggregate and compare readings with natural
/// arithmetic instead of verbose helper calls — e.g. the aggregate load of two smart
/// meters is simply <c>Meter3 = Meter1 + Meter2</c>, and delta detection is <c>now - prev</c>.
/// Implemented as a <c>readonly struct</c> so aggregation over large telemetry batches
/// stays allocation-free.
/// </summary>
public readonly struct SensorReading : IComparable<SensorReading>, IEquatable<SensorReading>
{
    public double Value { get; }
    public string Unit { get; }

    public SensorReading(double value, string unit = "")
    {
        Value = value;
        Unit = unit ?? string.Empty;
    }

    // --- Arithmetic operator overloading -------------------------------------------------
    // Aggregate load of two meters:  Meter3 = Meter1 + Meter2
    public static SensorReading operator +(SensorReading a, SensorReading b)
        => new(a.Value + b.Value, PickUnit(a, b));

    // Delta / drift between two readings:  drift = current - previous
    public static SensorReading operator -(SensorReading a, SensorReading b)
        => new(a.Value - b.Value, PickUnit(a, b));

    // Scale a reading (e.g. apply a calibration factor):  scaled = reading * 1.05
    public static SensorReading operator *(SensorReading a, double factor)
        => new(a.Value * factor, a.Unit);

    // --- Comparison operator overloading -------------------------------------------------
    public static bool operator >(SensorReading a, SensorReading b) => a.Value > b.Value;
    public static bool operator <(SensorReading a, SensorReading b) => a.Value < b.Value;
    public static bool operator >=(SensorReading a, SensorReading b) => a.Value >= b.Value;
    public static bool operator <=(SensorReading a, SensorReading b) => a.Value <= b.Value;
    public static bool operator ==(SensorReading a, SensorReading b) => a.Equals(b);
    public static bool operator !=(SensorReading a, SensorReading b) => !a.Equals(b);

    // --- Equality / comparison plumbing --------------------------------------------------
    public int CompareTo(SensorReading other) => Value.CompareTo(other.Value);
    public bool Equals(SensorReading other)
        => Value.Equals(other.Value) && string.Equals(Unit, other.Unit, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => obj is SensorReading r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(Value, Unit.ToLowerInvariant());

    public override string ToString()
        => string.IsNullOrEmpty(Unit)
            ? Value.ToString("0.###", CultureInfo.InvariantCulture)
            : $"{Value.ToString("0.###", CultureInfo.InvariantCulture)} {Unit}";

    private static string PickUnit(SensorReading a, SensorReading b)
        => string.IsNullOrEmpty(a.Unit) ? b.Unit : a.Unit;
}
