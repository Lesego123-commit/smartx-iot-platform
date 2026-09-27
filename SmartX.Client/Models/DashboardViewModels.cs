// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Client/Models/DashboardViewModels.cs
// Purpose: Presentation models for the device heat-strip and alert wall.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Data binding overview (WPF). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/ [Accessed 10 September 2026].
//   - Microsoft, 2024. Windows Presentation Foundation (WPF) documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/ [Accessed 10 September 2026].
// ------------------------------------------------------------------------------------
using System.Windows.Media;
using SmartX.Core;

namespace SmartX.Client.Models;

/// <summary>Presentation model for one device tile on the Anomaly Heat-Strip.</summary>
public sealed class DeviceTileVm
{
    public string DeviceId { get; init; } = "";
    public string ShortId { get; init; } = "";
    public string Location { get; init; } = "";
    public string Category { get; init; } = "";
    public string LastValue { get; init; } = "—";
    public string AnomalyText { get; init; } = "";
    public string StatusText { get; init; } = "";
    public Brush StatusBrush { get; init; } = Brushes.Gray;
    public Brush AccentBar { get; init; } = Brushes.Gray;

    public static DeviceTileVm From(DeviceHealth d)
    {
        var (brush, text) = Status(d);
        return new DeviceTileVm
        {
            DeviceId = d.DeviceId,
            ShortId = Short(d.DeviceId),
            Location = d.Location,
            Category = d.Category.ToString(),
            LastValue = d.LastValue is { } v ? v.ToString("0.##") : "—",
            AnomalyText = d.AnomalyCount > 0 ? $"{d.AnomalyCount} anomaly" : "clean",
            StatusText = text,
            StatusBrush = brush,
            AccentBar = brush
        };
    }

    private static (Brush, string) Status(DeviceHealth d)
    {
        if (d.IsSilent) return (Res("BadBrush"), "SILENT / DISCONNECTED");
        if (d.AnomalyCount > 0) return (Res("WarnBrush"), "ANOMALIES DETECTED");
        return (Res("GoodBrush"), "HEALTHY");
    }

    private static string Short(string id)
    {
        var parts = id.Split(':');
        return parts.Length >= 2 ? $"…{parts[^2]}:{parts[^1]}" : id;
    }

    internal static Brush Res(string key)
        => System.Windows.Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}

/// <summary>Presentation model for one entry on the Live Alert Wall.</summary>
public sealed class AlertVm
{
    public string Message { get; init; } = "";
    public string DeviceShort { get; init; } = "";
    public string TimeText { get; init; } = "";
    public string SeverityText { get; init; } = "";
    public Brush SeverityBrush { get; init; } = Brushes.Gray;

    public static AlertVm From(AlertDto a) => new()
    {
        Message = a.Message,
        DeviceShort = a.DeviceId,
        TimeText = a.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
        SeverityText = a.Severity.ToString().ToUpperInvariant(),
        SeverityBrush = a.Severity switch
        {
            AlertSeverity.Critical => DeviceTileVm.Res("BadBrush"),
            AlertSeverity.Warning => DeviceTileVm.Res("WarnBrush"),
            _ => DeviceTileVm.Res("AccentBrush")
        }
    };
}
