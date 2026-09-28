// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Client/Views/StartupView.xaml.cs
// Purpose: Startup view code-behind; raises the open-ingestion routed event.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Routed events overview (WPF). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/routed-events-overview [Accessed 10 September 2026].
//   - Microsoft, 2024. Windows Presentation Foundation (WPF) documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/ [Accessed 10 September 2026].
// ------------------------------------------------------------------------------------
using System.Windows.Controls;

namespace SmartX.Client.Views;

public partial class StartupView : UserControl
{
    /// <summary>Raised when the user opens the (active) Sensor Data Ingestion pillar.</summary>
    public event EventHandler? OpenIngestionRequested;

    /// <summary>Raised when the user opens the (active) Real-Time Command Stream pillar.</summary>
    public event EventHandler? OpenCommandStreamRequested;

    /// <summary>Raised when the user opens the (active) Network Topology &amp; Mesh Routing pillar.</summary>
    public event EventHandler? OpenTopologyRequested;

    public StartupView()
    {
        InitializeComponent();
    }

    private void OpenIngestion_Click(object sender, System.Windows.RoutedEventArgs e)
        => OpenIngestionRequested?.Invoke(this, EventArgs.Empty);

    private void OpenCommandStream_Click(object sender, System.Windows.RoutedEventArgs e)
        => OpenCommandStreamRequested?.Invoke(this, EventArgs.Empty);

    private void OpenTopology_Click(object sender, System.Windows.RoutedEventArgs e)
        => OpenTopologyRequested?.Invoke(this, EventArgs.Empty);
}
