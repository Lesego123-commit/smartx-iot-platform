// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X full suite (Ingestion + Command Stream + Topology)
// File:    SmartX.Client/MainWindow.xaml.cs
// Purpose: Shell code-behind: owns the API client and swaps the three pillar views. Views are
//          created once and reused, so switching between all three modules preserves their
//          state and never re-hits the backend unnecessarily.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Windows Presentation Foundation (WPF) documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/ [Accessed 25 September 2026].
//   - Microsoft, 2024. Navigation overview (WPF). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/app-development/navigation-overview [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Windows;
using SmartX.Client.Services;
using SmartX.Client.Views;

namespace SmartX.Client;

/// <summary>
/// Shell window. Owns the shared API client and swaps the architectural-pillar views into
/// the content host. Part 1 (Ingestion) and Part 2 (Command Stream) are active; Topology
/// remains locked for the final PoE. Each view is instantiated once and reused so that
/// navigating between modules preserves application state.
/// </summary>
public partial class MainWindow : Window
{
    // Matches the API's http launch profile (SmartX.Api/Properties/launchSettings.json).
    private const string ApiBaseUrl = "http://localhost:5101";

    private readonly SmartXApiClient _api;
    private readonly StartupView _startup;
    private IngestionView? _ingestion;
    private CommandStreamView? _commandStream;
    private TopologyView? _topology;

    public MainWindow()
    {
        InitializeComponent();
        _api = new SmartXApiClient(ApiBaseUrl);
        ApiFooter.Text = $"API: {ApiBaseUrl}";

        _startup = new StartupView();
        _startup.OpenIngestionRequested += (_, _) => ShowIngestion();
        _startup.OpenCommandStreamRequested += (_, _) => ShowCommandStream();
        _startup.OpenTopologyRequested += (_, _) => ShowTopology();
        Host.Content = _startup;
    }

    private void Home_Click(object sender, RoutedEventArgs e) => Host.Content = _startup;

    private void Ingestion_Click(object sender, RoutedEventArgs e) => ShowIngestion();

    private void CommandStream_Click(object sender, RoutedEventArgs e) => ShowCommandStream();

    private void Topology_Click(object sender, RoutedEventArgs e) => ShowTopology();

    private void ShowIngestion()
    {
        _ingestion ??= new IngestionView(_api); // created once, reused (state preserved)
        Host.Content = _ingestion;
    }

    private void ShowCommandStream()
    {
        _commandStream ??= new CommandStreamView(_api); // created once, reused (state preserved)
        Host.Content = _commandStream;
    }

    private void ShowTopology()
    {
        _topology ??= new TopologyView(_api); // created once, reused (state preserved)
        Host.Content = _topology;
    }
}
