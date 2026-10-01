// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
// File:    SmartX.Client/Models/CommandStreamViewModels.cs
// Purpose: Presentation models for the Command Stream view - device rows, predictive
//          insight cards and command-history (undo stack) rows, with pre-computed brushes.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Data binding overview (WPF). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/ [Accessed 25 September 2026].
//   - Microsoft, 2024. Data templating overview (WPF). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-templating-overview [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Windows.Media;
using SmartX.Core;

namespace SmartX.Client.Models;

/// <summary>A device row in the live command-stream list.</summary>
public sealed class DeviceRowVm
{
    public string DeviceId { get; init; } = "";
    public string ShortId { get; init; } = "";
    public string Location { get; init; } = "";
    public string Category { get; init; } = "";
    public string Reading { get; init; } = "";
    public string StateText { get; init; } = "";
    public Brush StateBrush { get; init; } = Brushes.Gray;
    public string PumpText { get; init; } = "";
    public string RelayText { get; init; } = "";

    public static DeviceRowVm From(DeviceStateDto d) => new()
    {
        DeviceId = d.DeviceId,
        ShortId = Short(d.DeviceId),
        Location = d.Location,
        Category = d.Category.ToString(),
        Reading = $"{d.PrimaryMetric}: {(d.LastValue is { } v ? v.ToString("0.##") : "-")}",
        StateText = d.AlertState.ToString().ToUpperInvariant(),
        StateBrush = Res(d.AlertState switch
        {
            DeviceAlertState.Critical => "BadBrush",
            DeviceAlertState.Disconnected => "BadBrush",
            DeviceAlertState.Warning => "WarnBrush",
            _ => "GoodBrush"
        }),
        PumpText = d.PumpOn ? "Pump ON" : "Pump off",
        RelayText = d.RelayOn ? "Relay ON" : "Relay off"
    };

    internal static string Short(string id)
    {
        var p = id.Split(':');
        return p.Length >= 2 ? $"{p[^2]}:{p[^1]}" : id;
    }

    internal static Brush Res(string key)
        => System.Windows.Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}

/// <summary>A predictive "Suggested Action" / "Automated Insight" card.</summary>
public sealed class InsightVm
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string? DeviceId { get; init; }
    public CommandAction? Action { get; init; }
    public bool HasAction => Action.HasValue;
    public string ActionText { get; init; } = "";
    public Brush AccentBrush { get; init; } = Brushes.Gray;

    public static InsightVm From(Insight i) => new()
    {
        Title = i.Title,
        Detail = i.Detail,
        DeviceId = i.DeviceId,
        Action = i.SuggestedAction,
        ActionText = i.SuggestedAction is { } a ? $"Apply: {a}" : "",
        AccentBrush = DeviceRowVm.Res(i.Severity switch
        {
            AlertSeverity.Critical => "BadBrush",
            AlertSeverity.Warning => "WarnBrush",
            _ => "AccentBrush"
        })
    };
}

/// <summary>A row in the command history (undo stack).</summary>
public sealed class CommandRowVm
{
    public string Summary { get; init; } = "";
    public string TimeText { get; init; } = "";
    public string StatusText { get; init; } = "";
    public double Opacity { get; init; } = 1.0;

    public static CommandRowVm From(CommandRecord c) => new()
    {
        Summary = c.Summary,
        TimeText = c.IssuedAt.ToLocalTime().ToString("HH:mm:ss"),
        StatusText = c.IsUndone ? "undone" : "active",
        Opacity = c.IsUndone ? 0.45 : 1.0
    };
}
