// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
// File:    SmartX.Client/Views/CommandStreamView.xaml.cs
// Purpose: Code-behind for the Real-Time Command Stream page. Async polling of the queue
//          status, device list, insights, history and errors; issues override commands and
//          undo; renders the sorted timeline as a sparkline.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Asynchronous programming with async and await (C#). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/ [Accessed 25 September 2026].
//   - Microsoft, 2024. DispatcherTimer Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer [Accessed 25 September 2026].
//   - Microsoft, 2024. Polyline Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.windows.shapes.polyline [Accessed 25 September 2026].
//   - Microsoft, 2024. ListBox Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.listbox [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SmartX.Client.Models;
using SmartX.Client.Services;
using SmartX.Core;

namespace SmartX.Client.Views;

public partial class CommandStreamView : UserControl
{
    private readonly SmartXApiClient _api;
    private readonly DispatcherTimer _timer;
    private bool _busy;
    private string? _selectedDeviceId;
    private List<DeviceStateDto> _allDevices = new();
    private List<TimelinePoint> _timeline = new();

    public CommandStreamView(SmartXApiClient api)
    {
        InitializeComponent();
        _api = api;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => { if (LiveToggle.IsChecked == true) await RefreshAsync(); };

        Loaded += async (_, _) => { await RefreshAsync(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    // ---- Refresh -----------------------------------------------------------------------
    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var status = await _api.GetStreamStatusAsync();
            if (status is null) { SetConnection(false); return; }
            SetConnection(true);

            KpiStdQueue.Text = status.StandardQueueDepth.ToString();
            KpiPriQueue.Text = status.PriorityQueueDepth.ToString();
            KpiProcessed.Text = (status.StandardProcessed + status.PriorityProcessed).ToString("N0");
            KpiErrors.Text = status.ActiveErrorCount.ToString();
            KpiHistory.Text = status.CommandHistoryDepth.ToString();

            _allDevices = (await _api.GetStreamDevicesAsync()).ToList();
            RebuildDeviceList(ApplyClientFilter(_allDevices));

            InsightList.ItemsSource = (await _api.GetInsightsAsync()).Select(InsightVm.From).ToList();
            HistoryList.ItemsSource = (await _api.GetCommandHistoryAsync(20)).Select(CommandRowVm.From).ToList();
            ErrorList.ItemsSource = await _api.GetActiveErrorsAsync();

            if (_selectedDeviceId is not null) await LoadTimelineAsync(_selectedDeviceId);
        }
        catch { SetConnection(false); }
        finally { _busy = false; }
    }

    // ---- Filtering ---------------------------------------------------------------------
    private (string? cat, string? alert) GetFilters()
    {
        string? cat = (CategoryFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        string? alert = (AlertFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return (cat == "All" ? null : cat, alert == "All" ? null : alert);
    }

    private List<DeviceStateDto> ApplyClientFilter(IEnumerable<DeviceStateDto> devices)
    {
        var (cat, alert) = GetFilters();
        IEnumerable<DeviceStateDto> q = devices;
        if (Enum.TryParse<SensorCategory>(cat, true, out var c)) q = q.Where(d => d.Category == c);
        if (Enum.TryParse<DeviceAlertState>(alert, true, out var a)) q = q.Where(d => d.AlertState == a);
        return q.ToList();
    }

    private async void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        // Server-side search (logged for the predictive engine), then show the result.
        var (cat, alert) = GetFilters();
        try
        {
            var list = await _api.SearchDevicesAsync(cat, alert);
            RebuildDeviceList(list);
        }
        catch { /* connection handled elsewhere */ }
    }

    private void RebuildDeviceList(List<DeviceStateDto> devices)
    {
        var selected = _selectedDeviceId;
        DeviceList.ItemsSource = devices.Select(DeviceRowVm.From).ToList();
        if (selected is not null)
        {
            var match = DeviceList.Items.Cast<DeviceRowVm>().FirstOrDefault(r => r.DeviceId == selected);
            if (match is not null) DeviceList.SelectedItem = match;
        }
    }

    // ---- Selection + timeline ----------------------------------------------------------
    private async void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceList.SelectedItem is not DeviceRowVm row) return;
        _selectedDeviceId = row.DeviceId;
        TimelineSubtitle.Text = row.ShortId;
        await LoadTimelineAsync(row.DeviceId);
    }

    private async Task LoadTimelineAsync(string deviceId)
    {
        _timeline = (await _api.GetTimelineAsync(deviceId, 60)).ToList();
        DrawTimeline();
    }

    private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTimeline();

    private void DrawTimeline()
    {
        TimelineCanvas.Children.Clear();
        double w = TimelineCanvas.ActualWidth, h = TimelineCanvas.ActualHeight;
        if (_timeline.Count < 2 || w < 10 || h < 10) return;

        double min = _timeline.Min(p => p.Value), max = _timeline.Max(p => p.Value);
        double range = Math.Abs(max - min) < 1e-6 ? 1 : max - min;
        double pad = 6;

        var line = new Polyline
        {
            Stroke = (Brush)FindResource("AccentBrush"),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        for (int i = 0; i < _timeline.Count; i++)
        {
            double x = pad + (w - 2 * pad) * i / (_timeline.Count - 1);
            double y = pad + (h - 2 * pad) * (1 - (_timeline[i].Value - min) / range);
            line.Points.Add(new Point(x, y));
        }
        TimelineCanvas.Children.Add(line);

        // baseline + latest-value marker
        var last = line.Points[^1];
        TimelineCanvas.Children.Add(new Ellipse
        {
            Width = 7, Height = 7, Fill = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(last.X - 3.5, last.Y - 3.5, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        });
    }

    // ---- Commands ----------------------------------------------------------------------
    private async void Pump_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string id) return;
        var dev = _allDevices.FirstOrDefault(d => d.DeviceId == id);
        var action = dev is { PumpOn: true } ? CommandAction.StopPump : CommandAction.TriggerPump;
        await IssueAsync(id, action);
    }

    private async void Relay_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string id) await IssueAsync(id, CommandAction.ToggleRelay);
    }

    private async void ApplyInsight_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is InsightVm vm && vm.DeviceId is not null && vm.Action is { } a)
            await IssueAsync(vm.DeviceId, a);
    }

    private async Task IssueAsync(string deviceId, CommandAction action)
    {
        try { await _api.IssueCommandAsync(deviceId, action); await RefreshAsync(); }
        catch { SetConnection(false); }
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        try { await _api.UndoLastCommandAsync(); await RefreshAsync(); }
        catch { SetConnection(false); }
    }

    // ---- Queue processing --------------------------------------------------------------
    private async void ProcessNext_Click(object sender, RoutedEventArgs e)
    {
        try { await _api.ProcessStreamAsync(all: false); await RefreshAsync(); }
        catch { SetConnection(false); }
    }

    private async void ProcessAll_Click(object sender, RoutedEventArgs e)
    {
        try { await _api.ProcessStreamAsync(all: true); await RefreshAsync(); }
        catch { SetConnection(false); }
    }

    // ---- helpers -----------------------------------------------------------------------
    private void SetConnection(bool ok)
    {
        ConnText.Text = ok ? $"Connected · {_api.BaseAddress}" : "Gateway offline";
        ConnDot.Fill = (Brush)(ok ? FindResource("GoodBrush") : FindResource("BadBrush"));
    }
}
