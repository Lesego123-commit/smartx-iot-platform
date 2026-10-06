// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Client/Views/TopologyView.xaml.cs
// Purpose: Code-behind for the Network Topology page. Fetches the mesh graph and renders it
//          on a Canvas (nodes + weighted edges), highlights the MST and traced routes, runs
//          BFS/DFS traversal, and queries the AVL history by time range.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Shapes and basic drawing in WPF overview. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/shapes-and-basic-drawing-in-wpf-overview [Accessed 25 September 2026].
//   - Microsoft, 2024. Canvas Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.canvas [Accessed 25 September 2026].
//   - Microsoft, 2024. Asynchronous programming with async and await (C#). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/ [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SmartX.Client.Services;
using SmartX.Core;

namespace SmartX.Client.Views;

/// <summary>A row in the historical-log list (AVL range-query result).</summary>
public sealed class HistoryRowVm
{
    public string TimeText { get; init; } = "";
    public string ValueText { get; init; } = "";
}

public partial class TopologyView : UserControl
{
    private readonly SmartXApiClient _api;
    private MeshGraphDto? _graph;
    private bool _showMst;
    private List<string> _routeHops = new();
    private HashSet<string> _unreachable = new(StringComparer.OrdinalIgnoreCase);
    private bool _traversalActive;

    public TopologyView(SmartXApiClient api)
    {
        InitializeComponent();
        _api = api;
        Loaded += async (_, _) => await LoadGraphAsync();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadGraphAsync();

    private async Task LoadGraphAsync()
    {
        try
        {
            _graph = await _api.GetMeshGraphAsync();
            if (_graph is null) { SetConnection(false); return; }
            SetConnection(true);

            var sensors = _graph.Nodes.Where(n => !n.IsGateway).ToList();
            SetupCombo(RouteDevice, sensors);
            SetupCombo(HistoryDevice, sensors);
            GraphSubtitle.Text = $"{_graph.Nodes.Count} nodes · {_graph.Edges.Count} links · MST weight {_graph.MstTotalWeight:0.#}";
            DrawGraph();
        }
        catch { SetConnection(false); }
    }

    private static void SetupCombo(ComboBox combo, List<MeshNodeDto> sensors)
    {
        var current = combo.SelectedValue as string;
        combo.DisplayMemberPath = "Label";
        combo.SelectedValuePath = "Id";
        combo.ItemsSource = sensors;
        if (current is not null) combo.SelectedValue = current;
        else if (sensors.Count > 0) combo.SelectedIndex = 0;
    }

    // ---- Actions -----------------------------------------------------------------------
    private async void TraceRoute_Click(object sender, RoutedEventArgs e)
    {
        if (RouteDevice.SelectedValue is not string id) return;
        try
        {
            var route = await _api.GetRouteAsync(id);
            _traversalActive = false; _unreachable.Clear();
            if (route is null || !route.Reachable || route.Hops.Count == 0)
            {
                _routeHops = new();
                RouteResult.Text = "No route — the node is down or isolated from the gateway.";
            }
            else
            {
                _routeHops = route.Hops.ToList();
                var path = string.Join(" → ", route.Hops.Select(Short));
                RouteResult.Text = $"{path}\nTotal routing cost: {route.TotalCost:0.#} ({route.Hops.Count - 1} hops).";
            }
            DrawGraph();
        }
        catch { SetConnection(false); }
    }

    private async void Bfs_Click(object sender, RoutedEventArgs e) => await Traverse("bfs");
    private async void Dfs_Click(object sender, RoutedEventArgs e) => await Traverse("dfs");

    private async Task Traverse(string kind)
    {
        try
        {
            var t = await _api.TraverseAsync(kind);
            if (t is null) return;
            _routeHops = new();
            _traversalActive = true;
            _unreachable = new HashSet<string>(t.Unreachable, StringComparer.OrdinalIgnoreCase);
            AnalysisResult.Text = $"{t.Kind} from gateway reached {t.Reachable.Count} node(s). " +
                                  (t.Unreachable.Count == 0 ? "All nodes reachable."
                                     : $"Down/unreachable: {string.Join(", ", t.Unreachable.Select(Short))}.");
            DrawGraph();
        }
        catch { SetConnection(false); }
    }

    private void Mst_Click(object sender, RoutedEventArgs e)
    {
        _showMst = !_showMst;
        MstButton.Content = _showMst ? "Hide MST" : "Show MST";
        if (_showMst && _graph is not null)
            AnalysisResult.Text = $"Minimum Spanning Tree (Prim's) highlighted — total power-efficient routing cost {_graph.MstTotalWeight:0.#}.";
        DrawGraph();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _routeHops = new(); _traversalActive = false; _unreachable.Clear(); _showMst = false;
        MstButton.Content = "Show MST";
        AnalysisResult.Text = ""; RouteResult.Text = "";
        DrawGraph();
    }

    private async void History_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryDevice.SelectedValue is not string id) return;
        int minutes = (HistoryWindow.SelectedIndex switch { 0 => 10, 1 => 20, 2 => 30, _ => 60 });
        try
        {
            var to = DateTimeOffset.UtcNow;
            var from = to.AddMinutes(-minutes);
            var points = await _api.GetHistoryAsync(id, from, to);
            HistoryList.ItemsSource = points.Select(p => new HistoryRowVm
            {
                TimeText = p.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                ValueText = p.Value.ToString("0.##", CultureInfo.InvariantCulture)
            }).ToList();
            HistoryResult.Text = $"AVL range query returned {points.Count} reading(s) in the last {minutes} min (ascending by time).";
        }
        catch { SetConnection(false); }
    }

    // ---- Drawing -----------------------------------------------------------------------
    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGraph();

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();
        if (_graph is null) return;
        double w = GraphCanvas.ActualWidth, h = GraphCanvas.ActualHeight;
        if (w < 20 || h < 20) return;
        double pad = 46;

        Point P(MeshNodeDto n) => new(pad + (w - 2 * pad) * n.X, pad + (h - 2 * pad) * n.Y);
        var pos = _graph.Nodes.ToDictionary(n => n.Id, P, StringComparer.OrdinalIgnoreCase);

        var routeEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < _routeHops.Count; i++)
            routeEdges.Add(EdgeKey(_routeHops[i], _routeHops[i + 1]));

        var grey = (Brush)FindResource("BorderBrush");
        var accent = (Brush)FindResource("AccentBrush");
        var warn = (Brush)FindResource("WarnBrush");
        var good = (Brush)FindResource("GoodBrush");
        var bad = (Brush)FindResource("BadBrush");
        var muted = (Brush)FindResource("MutedBrush");

        // Edges
        foreach (var e in _graph.Edges)
        {
            if (!pos.TryGetValue(e.FromId, out var a) || !pos.TryGetValue(e.ToId, out var b)) continue;
            bool onRoute = routeEdges.Contains(EdgeKey(e.FromId, e.ToId));
            bool mst = _showMst && e.InMst;
            var line = new Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y,
                Stroke = onRoute ? warn : mst ? accent : grey,
                StrokeThickness = onRoute ? 4 : mst ? 2.5 : 1,
                Opacity = onRoute || mst ? 1 : 0.5
            };
            GraphCanvas.Children.Add(line);
        }

        // Nodes
        foreach (var n in _graph.Nodes)
        {
            var p = pos[n.Id];
            double r = n.IsGateway ? 20 : 15;
            bool unreachable = _traversalActive && _unreachable.Contains(n.Id);
            Brush fill = n.IsGateway ? accent : (n.IsDown || unreachable) ? bad : good;
            bool onRoute = _routeHops.Contains(n.Id);

            var dot = new Ellipse
            {
                Width = r * 2, Height = r * 2, Fill = fill,
                Stroke = onRoute ? warn : Brushes.Transparent,
                StrokeThickness = onRoute ? 3 : 0,
                Tag = n.Id, Cursor = System.Windows.Input.Cursors.Hand
            };
            dot.MouseLeftButtonUp += Node_Click;
            Canvas.SetLeft(dot, p.X - r);
            Canvas.SetTop(dot, p.Y - r);
            GraphCanvas.Children.Add(dot);

            var label = new TextBlock
            {
                Text = n.Label, FontSize = 10.5, Foreground = muted,
                TextAlignment = TextAlignment.Center, Width = 96
            };
            Canvas.SetLeft(label, p.X - 48);
            Canvas.SetTop(label, p.Y + r + 2);
            GraphCanvas.Children.Add(label);
        }
    }

    private void Node_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as Ellipse)?.Tag is not string id) return;
        RouteDevice.SelectedValue = id;
        HistoryDevice.SelectedValue = id;
        TraceRoute_Click(this, new RoutedEventArgs());
    }

    // ---- helpers -----------------------------------------------------------------------
    private static string EdgeKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    private static string Short(string id)
    {
        if (id.StartsWith("GATEWAY", StringComparison.OrdinalIgnoreCase)) return "Gateway";
        var p = id.Split(':');
        return p.Length >= 2 ? $"{p[^2]}:{p[^1]}" : id;
    }

    private void SetConnection(bool ok)
    {
        ConnText.Text = ok ? $"Connected · {_api.BaseAddress}" : "Gateway offline";
        ConnDot.Fill = (Brush)(ok ? FindResource("GoodBrush") : FindResource("BadBrush"));
    }
}
