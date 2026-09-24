// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Core/HistoricalBatch.cs
// Purpose: Bridges jagged/multidimensional raw telemetry arrays into List<T>.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Arrays (C# Programming Guide) - jagged and multidimensional arrays. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/arrays/ [Accessed 10 September 2026].
//   - Microsoft, 2024. List<T> Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1 [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
namespace SmartX.Core;

/// <summary>
/// Bridges raw telemetry (which arrives from the gateway as multi-dimensional and jagged
/// arrays of primitives) into optimised <see cref="List{T}"/> collections that the rest of
/// the pipeline works with.
///
/// Demonstrates the "advanced arrays -&gt; collections" requirement: sequential historical
/// batches are held as raw arrays for cheap bulk transfer, then flattened once into a
/// growable, queryable List for downstream analysis.
/// </summary>
public static class HistoricalBatch
{
    /// <summary>
    /// Jagged array: one inner array per polling cycle, each of a different length
    /// (bursty devices report more samples). Flattened into a single List.
    /// </summary>
    public static List<SensorReading> FromJagged(double[][] cycles, string unit)
    {
        var readings = new List<SensorReading>();
        foreach (double[] cycle in cycles)          // outer: polling cycles
            foreach (double raw in cycle)            // inner: variable-length samples
                readings.Add(new SensorReading(raw, unit));
        return readings;
    }

    /// <summary>
    /// Rectangular multi-dimensional array [device, timeSlot] — a fixed grid of readings.
    /// Flattened row-major into a List, tagging each with its originating device index.
    /// </summary>
    public static List<SensorReading> FromGrid(double[,] grid, string unit)
    {
        int devices = grid.GetLength(0);
        int slots = grid.GetLength(1);
        var readings = new List<SensorReading>(devices * slots);
        for (int d = 0; d < devices; d++)
            for (int t = 0; t < slots; t++)
                readings.Add(new SensorReading(grid[d, t], unit));
        return readings;
    }

    /// <summary>Aggregate load across a batch using overloaded '+' on SensorReading.</summary>
    public static SensorReading AggregateLoad(IEnumerable<SensorReading> readings, string unit)
    {
        var total = new SensorReading(0, unit);
        foreach (var r in readings)
            total = total + r;   // operator overloading in action
        return total;
    }
}
