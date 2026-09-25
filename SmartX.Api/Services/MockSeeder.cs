// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Api/Services/MockSeeder.cs
// Purpose: Seeded synthetic telemetry generator with injected anomalies.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Random Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.random [Accessed 10 September 2026].
//   - Microsoft, 2024. Arrays (C# Programming Guide) - jagged and multidimensional arrays. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/arrays/ [Accessed 10 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using SmartX.Core;

namespace SmartX.Api.Services;

/// <summary>
/// Seeds the store with a realistic, repeatable stream of mock telemetry so the data
/// structures are exercised "under load" (per the brief). Deliberately injects two
/// spikes and one sensor disconnect so the anomaly engine and engagement dashboard have
/// something to surface out of the box.
/// </summary>
public static class MockSeeder
{
    private sealed record Device(
        string Mac, string Location, SensorCategory Category,
        string Metric, TelemetryKind Kind, double Baseline, double Noise);

    public static void Seed(TelemetryStore store)
    {
        var rng = new Random(20260728); // seeded => repeatable demo
        var now = DateTimeOffset.UtcNow;
        const int minutes = 45;

        var devices = new[]
        {
            new Device("3C:71:BF:0A:2D:01", "Facility A / Zone 1 / Sub-Zone B", SensorCategory.Environmental,   "soil_moisture", TelemetryKind.Float, 42, 2.5),
            new Device("3C:71:BF:0A:2D:02", "Facility A / Zone 1 / Greenhouse",  SensorCategory.Environmental,   "soil_moisture", TelemetryKind.Float, 55, 3.0),
            new Device("3C:71:BF:0A:2D:03", "Facility A / Zone 2 / Roof",        SensorCategory.Environmental,   "temperature",   TelemetryKind.Float, 24, 1.5),
            new Device("3C:71:BF:0A:2D:04", "Facility A / Zone 2 / Corridor",    SensorCategory.Environmental,   "humidity",      TelemetryKind.Float, 60, 4.0),
            new Device("3C:71:BF:0A:2D:05", "Substation / Feeder 1",             SensorCategory.PowerConsumption,"power_wattage", TelemetryKind.Int,   1200, 90),
            new Device("3C:71:BF:0A:2D:06", "Substation / Feeder 2",             SensorCategory.PowerConsumption,"power_wattage", TelemetryKind.Int,   1500, 110),
            new Device("3C:71:BF:0A:2D:07", "Substation / Main Bus",             SensorCategory.PowerConsumption,"voltage",       TelemetryKind.Float, 230, 2.0),
            new Device("3C:71:BF:0A:2D:08", "Pump House / Line 1",               SensorCategory.Actuator,        "valve_state",   TelemetryKind.Bool,  0, 0),
            new Device("3C:71:BF:0A:2D:09", "Pump House / Line 2",               SensorCategory.Actuator,        "relay_state",   TelemetryKind.Bool,  0, 0),
            new Device("3C:71:BF:0A:2D:0A", "Facility A / Zone 3 / Cold Store",  SensorCategory.Environmental,   "temperature",   TelemetryKind.Float, 4, 0.8),
        };

        foreach (var d in devices)
        {
            store.Register(new RegisterSensorRequest(d.Mac, d.Location, d.Category,
                $"Auto-seeded {d.Category} node emitting {d.Metric}."));

            bool goesSilent = d.Mac.EndsWith("2D:03"); // this temperature probe disconnects mid-run
            int silentAfter = 20;

            for (int i = 0; i < minutes; i++)
            {
                if (goesSilent && i > silentAfter) break; // simulate a sensor disconnect

                var ts = now.AddMinutes(-(minutes - i));
                var req = BuildSample(d, i, rng);
                store.Ingest(req, ts);
            }
        }
    }

    private static IngestRequest BuildSample(Device d, int i, Random rng)
    {
        double noise = (rng.NextDouble() - 0.5) * 2 * d.Noise;
        double wave = Math.Sin(i / 6.0) * d.Noise * 0.6;

        switch (d.Kind)
        {
            case TelemetryKind.Bool:
                bool state = rng.NextDouble() > 0.5;
                return new IngestRequest(d.Mac, d.Metric, TelemetryKind.Bool, 0, state);

            case TelemetryKind.Int:
            {
                double v = d.Baseline + wave + noise;
                // Inject a hard power spike on Feeder 1 around sample 30.
                if (d.Mac.EndsWith("2D:05") && i is >= 30 and <= 32)
                    v = d.Baseline * 3.4;
                return new IngestRequest(d.Mac, d.Metric, TelemetryKind.Int, v, false);
            }

            default: // Float
            {
                double v = d.Baseline + wave + noise;
                // Inject a soil-moisture crash on node 2D:01 around sample 35 (line break).
                if (d.Mac.EndsWith("2D:01") && i is >= 35 and <= 37)
                    v = d.Baseline * 0.15;
                return new IngestRequest(d.Mac, d.Metric, TelemetryKind.Float, v, false);
            }
        }
    }

    /// <summary>A sample multi-tier deployment tree used by the topology validator demo.</summary>
    public static DeploymentNode SampleTopology()
    {
        return new DeploymentNode("Facility A")
            .Add(new DeploymentNode("Zone 1")
                .Add(new DeploymentNode("Sub-Zone B")
                    .Add(new DeploymentNode("Soil Probe", "3C:71:BF:0A:2D:01"))
                    .Add(new DeploymentNode("Greenhouse Probe", "3C:71:BF:0A:2D:02")))
                .Add(new DeploymentNode("Roof Probe", "3C:71:BF:0A:2D:03")))
            .Add(new DeploymentNode("Zone 2")
                .Add(new DeploymentNode("Corridor Probe", "3C:71:BF:0A:2D:04")))
            .Add(new DeploymentNode("Substation")
                .Add(new DeploymentNode("Feeder 1 Meter", "3C:71:BF:0A:2D:05"))
                .Add(new DeploymentNode("Feeder 2 Meter", "3C:71:BF:0A:2D:06")));
    }
}
