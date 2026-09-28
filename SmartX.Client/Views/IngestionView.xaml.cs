// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Client/Views/IngestionView.xaml.cs
// Purpose: Ingestion code-behind: async API calls, file dialog and timer refresh.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. OpenFileDialog Class (Microsoft.Win32). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.openfiledialog [Accessed 10 September 2026].
//   - Microsoft, 2024. DispatcherTimer Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer [Accessed 10 September 2026].
//   - Microsoft, 2024. Asynchronous programming with async and await (C#). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/ [Accessed 10 September 2026].
//   - Microsoft, 2024. MultipartFormDataContent Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.multipartformdatacontent [Accessed 10 September 2026].
// ------------------------------------------------------------------------------------
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using SmartX.Client.Models;
using SmartX.Client.Services;
using SmartX.Core;

namespace SmartX.Client.Views;

public partial class IngestionView : UserControl
{
    private readonly SmartXApiClient _api;
    private readonly DispatcherTimer _timer;
    private string? _selectedFilePath;
    private bool _busyRefresh;

    public IngestionView(SmartXApiClient api)
    {
        InitializeComponent();
        _api = api;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => { if (LiveToggle.IsChecked == true) await RefreshAsync(); };

        Loaded += async (_, _) =>
        {
            await RefreshDevicesAsync();
            await RefreshAsync();
            _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    // ---- Registration ------------------------------------------------------------------
    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var category = Enum.Parse<SensorCategory>(((ComboBoxItem)CategoryCombo.SelectedItem).Content!.ToString()!);
            var req = new RegisterSensorRequest(MacBox.Text.Trim(), LocationBox.Text.Trim(), category,
                string.IsNullOrWhiteSpace(DescBox.Text) ? null : DescBox.Text.Trim());

            var profile = await _api.RegisterAsync(req);
            SetStatus(RegStatus, $"✓ Registered {profile.Id} in {profile.Location}.", "GoodBrush");
            await RefreshDevicesAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus(RegStatus, $"✗ {ex.Message}", "BadBrush");
        }
    }

    // ---- File attachment (OpenFileDialog -> multipart upload) ---------------------------
    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select a device config, hardware log or deployment photo",
            Filter = "Config & logs (*.json;*.yaml;*.yml;*.log;*.txt;*.cfg)|*.json;*.yaml;*.yml;*.log;*.txt;*.cfg|" +
                     "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            _selectedFilePath = dlg.FileName;
            var info = new System.IO.FileInfo(dlg.FileName);
            SelectedFileText.Text = $"{info.Name}  ({info.Length:N0} bytes)";
        }
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (UploadDeviceCombo.SelectedItem is not string device)
        {
            SetStatus(UploadStatus, "✗ Select a target device first.", "BadBrush");
            return;
        }
        if (string.IsNullOrEmpty(_selectedFilePath))
        {
            SetStatus(UploadStatus, "✗ Choose a file first.", "BadBrush");
            return;
        }
        try
        {
            var profile = await _api.UploadAttachmentAsync(device, _selectedFilePath);
            SetStatus(UploadStatus, $"✓ Uploaded {profile.AttachmentName} ({profile.AttachmentBytes:N0} bytes).", "GoodBrush");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus(UploadStatus, $"✗ {ex.Message}", "BadBrush");
        }
    }

    // ---- Push telemetry ----------------------------------------------------------------
    private async void Ingest_Click(object sender, RoutedEventArgs e)
    {
        if (IngestDeviceCombo.SelectedItem is not string device)
        {
            SetStatus(IngestStatus, "✗ Select a device first.", "BadBrush");
            return;
        }
        try
        {
            var kind = Enum.Parse<TelemetryKind>(((ComboBoxItem)KindCombo.SelectedItem).Content!.ToString()!);
            double numeric = 0;
            bool boolean = false;

            if (kind == TelemetryKind.Bool)
                boolean = ValueBox.Text.Trim().ToLowerInvariant() is "true" or "1" or "on" or "yes";
            else if (!double.TryParse(ValueBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out numeric))
            {
                SetStatus(IngestStatus, "✗ Value must be a number for Float/Int.", "BadBrush");
                return;
            }

            var sample = await _api.IngestAsync(new IngestRequest(device, MetricBox.Text.Trim(), kind, numeric, boolean));
            var tag = sample.IsAnomaly ? " ⚠ ANOMALY" : "";
            SetStatus(IngestStatus, $"✓ {sample.Metric} = {sample.DisplayValue} ({sample.PayloadType}){tag}",
                sample.IsAnomaly ? "WarnBrush" : "GoodBrush");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus(IngestStatus, $"✗ {ex.Message}", "BadBrush");
        }
    }

    // ---- Dashboard refresh -------------------------------------------------------------
    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await _api.GetSensorsAsync();
            var ids = devices.Select(d => d.Id).ToList();
            PreserveSelection(UploadDeviceCombo, ids);
            PreserveSelection(IngestDeviceCombo, ids);
        }
        catch { /* connection handled by RefreshAsync */ }
    }

    private static void PreserveSelection(ComboBox combo, List<string> ids)
    {
        var current = combo.SelectedItem as string;
        combo.ItemsSource = ids;
        if (current is not null && ids.Contains(current)) combo.SelectedItem = current;
        else if (ids.Count > 0 && combo.SelectedItem is null) combo.SelectedIndex = 0;
    }

    private async Task RefreshAsync()
    {
        if (_busyRefresh) return;
        _busyRefresh = true;
        try
        {
            var snap = await _api.GetSnapshotAsync();
            if (snap is null) { SetConnection(false); return; }
            SetConnection(true);

            KpiDevices.Text = snap.RegisteredDevices.ToString();
            KpiSamples.Text = snap.TotalSamples.ToString("N0");
            KpiAnomalies.Text = snap.AnomalyCount.ToString();
            KpiSilent.Text = snap.SilentDevices.ToString();
            KpiRate.Text = snap.IngestRatePerMin.ToString("0");

            DeviceStrip.ItemsSource = snap.Devices.Select(DeviceTileVm.From).ToList();
            AlertWall.ItemsSource = snap.RecentAlerts.Select(AlertVm.From).ToList();
        }
        catch
        {
            SetConnection(false);
        }
        finally { _busyRefresh = false; }
    }

    // ---- Small UI helpers --------------------------------------------------------------
    private void SetConnection(bool ok)
    {
        ConnText.Text = ok ? $"Connected · {_api.BaseAddress}" : "Gateway offline";
        var brush = (Brush)(ok ? FindResource("GoodBrush") : FindResource("BadBrush"));
        ConnDot.Fill = brush;
    }

    private void SetStatus(TextBlock target, string message, string brushKey)
    {
        target.Text = message;
        target.Foreground = (Brush)FindResource(brushKey);
    }
}
