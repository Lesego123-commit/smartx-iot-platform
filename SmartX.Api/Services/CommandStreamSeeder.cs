// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
// File:    SmartX.Api/Services/CommandStreamSeeder.cs
// Purpose: Seed the CommandStreamEngine with live devices, an ordered timeline, a backlog
//          of queued packets (incl. critical ones on the priority queue), plus learned
//          command patterns so the Undo history and Suggested Actions have content on boot.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Random Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.random [Accessed 25 September 2026].
//   - Microsoft, 2024. Collections and Data Structures. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/standard/collections/ [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using SmartX.Core;

namespace SmartX.Api.Services;

public static class CommandStreamSeeder
{
    private sealed record DeviceSeed(string Mac, string Location, SensorCategory Category,
        string Metric, double Value, DeviceAlertState State);

    public static void Seed(CommandStreamEngine engine)
    {
        var rng = new Random(20260925);
        var now = DateTimeOffset.UtcNow;

        // Mirrors the Part 1 mesh, with the injected anomalies reflected as alert states.
        var devices = new[]
        {
            new DeviceSeed("3C:71:BF:0A:2D:01", "Facility A / Zone 1 / Sub-Zone B", SensorCategory.Environmental,   "soil_moisture", 6.3,  DeviceAlertState.Critical),
            new DeviceSeed("3C:71:BF:0A:2D:02", "Facility A / Zone 1 / Greenhouse",  SensorCategory.Environmental,   "soil_moisture", 54.8, DeviceAlertState.Normal),
            new DeviceSeed("3C:71:BF:0A:2D:03", "Facility A / Zone 2 / Roof",        SensorCategory.Environmental,   "temperature",   24.1, DeviceAlertState.Disconnected),
            new DeviceSeed("3C:71:BF:0A:2D:04", "Facility A / Zone 2 / Corridor",    SensorCategory.Environmental,   "humidity",      61.0, DeviceAlertState.Normal),
            new DeviceSeed("3C:71:BF:0A:2D:05", "Substation / Feeder 1",             SensorCategory.PowerConsumption,"power_wattage", 4080, DeviceAlertState.Critical),
            new DeviceSeed("3C:71:BF:0A:2D:06", "Substation / Feeder 2",             SensorCategory.PowerConsumption,"power_wattage", 1520, DeviceAlertState.Warning),
            new DeviceSeed("3C:71:BF:0A:2D:07", "Substation / Main Bus",             SensorCategory.PowerConsumption,"voltage",       230,  DeviceAlertState.Normal),
            new DeviceSeed("3C:71:BF:0A:2D:08", "Pump House / Line 1",               SensorCategory.Actuator,        "valve_state",   1,    DeviceAlertState.Normal),
            new DeviceSeed("3C:71:BF:0A:2D:09", "Pump House / Line 2",               SensorCategory.Actuator,        "relay_state",   0,    DeviceAlertState.Normal),
            new DeviceSeed("3C:71:BF:0A:2D:0A", "Facility A / Zone 3 / Cold Store",  SensorCategory.Environmental,   "temperature",   4.1,  DeviceAlertState.Normal),
        };

        foreach (var d in devices)
        {
            engine.Register(d.Mac, d.Location, d.Category, d.Metric, d.Value, d.State);

            // Build ~40 minutes of ordered history so timelines render.
            for (int i = 40; i >= 1; i--)
            {
                double baseline = d.Value == 0 ? 1 : d.Value;
                double noise = (rng.NextDouble() - 0.5) * baseline * 0.08;
                double v = d.State == DeviceAlertState.Critical && i > 5 ? baseline * 6 + noise : baseline + noise;
                var ts = now.AddMinutes(-i);
                engine.Ingest(new TelemetrySample(d.Mac, d.Metric, "Double", v.ToString("0.##"), v, ts, false), critical: false);
            }
        }

        // The historical points above have been processed already - drain them so the queue
        // depths reflect only the live backlog below.
        engine.ProcessAll();

        // A couple of live critical packets sitting on the PRIORITY queue (bypass the FIFO stream).
        engine.Ingest(new TelemetrySample("3C:71:BF:0A:2D:05", "power_wattage", "Int32", "4120", 4120, now, false), critical: true);
        engine.Ingest(new TelemetrySample("3C:71:BF:0A:2D:01", "soil_moisture", "Single", "5.9", 5.9, now, false), critical: true);

        // A backlog of standard packets waiting in the FIFO queue.
        for (int i = 0; i < 6; i++)
            engine.Ingest(new TelemetrySample("3C:71:BF:0A:2D:04", "humidity", "Single", "60", 60 + i, now, false), critical: false);

        // Teach the engine the operator's habits (so Suggested Actions appear immediately).
        engine.SeedPattern("low:soil_moisture", CommandAction.TriggerPump, weight: 4);
        engine.SeedPattern("low:power_wattage", CommandAction.ShutValve, weight: 2);
        engine.SeedPattern("low:power_wattage", CommandAction.AcknowledgeAlert, weight: 3);

        // A little pre-existing command history so the Undo stack isn't empty on boot.
        engine.IssueCommand(new DeviceCommand("3C:71:BF:0A:2D:08", CommandAction.OpenValve));
        engine.IssueCommand(new DeviceCommand("3C:71:BF:0A:2D:09", CommandAction.ToggleRelay));
    }
}
