// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Smart-X Gateway API (Parts 1-3)
// File:    SmartX.Api/Program.cs
// Purpose: ASP.NET Core Minimal API host for the full Smart-X suite: Part 1 ingestion/
//          validation, Part 2 Real-Time Command Stream (queues/commands/insights), and
//          Part 3 mesh topology (graph, BFS/DFS, MST, routing, AVL history). CORS, JSON, DI.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Minimal APIs overview. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis [Accessed 25 September 2026].
//   - Microsoft, 2024. Tutorial: Create a minimal API with ASP.NET Core. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api [Accessed 25 September 2026].
//   - Microsoft, 2024. Upload files in ASP.NET Core. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads [Accessed 25 September 2026].
//   - Microsoft, 2024. Enable Cross-Origin Requests (CORS) in ASP.NET Core. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/cors [Accessed 25 September 2026].
//   - Microsoft, 2024. Dependency injection in ASP.NET Core. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection [Accessed 25 September 2026].
//   - Microsoft, 2024. How to serialize and deserialize JSON using System.Text.Json. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/ [Accessed 25 September 2026].
// ------------------------------------------------------------------------------------
using System.Text.Json.Serialization;
using SmartX.Api.Services;
using SmartX.Core;

var builder = WebApplication.CreateBuilder(args);

// Serialise enums as strings so the WPF client and API share one wire contract.
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// Single shared in-memory processing engines (Part 1 ingestion, Part 2 command stream,
// Part 3 mesh topology).
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<CommandStreamEngine>();
builder.Services.AddSingleton<TopologyEngine>();

// Allow the desktop client (any local origin) to call the gateway.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors();

// Uploaded config files / deployment photos / hardware logs live here.
var uploadRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadRoot);

// Heavily seed mock telemetry + command-stream state on boot.
MockSeeder.Seed(app.Services.GetRequiredService<TelemetryStore>());
CommandStreamSeeder.Seed(app.Services.GetRequiredService<CommandStreamEngine>());

// ----------------------------------------------------------------------------------------
// Meta
// ----------------------------------------------------------------------------------------
app.MapGet("/", () => Results.Json(new
{
    service = "Smart-X Gateway (Part 1 ingestion + Part 2 command stream + Part 3 mesh topology)",
    status = "online",
    version = "3.0",
    endpoints = new[]
    {
        "GET /api/health", "GET/POST /api/sensors", "POST /api/sensors/{id}/attachment",
        "POST /api/telemetry", "GET /api/telemetry", "GET /api/snapshot", "GET /api/alerts",
        "POST /api/topology/validate", "GET /api/aggregate",
        "GET /api/stream/status", "GET /api/stream/devices", "GET /api/stream/search",
        "POST /api/stream/process", "POST /api/commands", "POST /api/commands/undo",
        "GET /api/commands/history", "GET /api/stream/timeline", "GET /api/stream/errors",
        "GET /api/stream/insights"
    }
}));

app.MapGet("/api/health", () => Results.Ok(new { ok = true, time = DateTimeOffset.UtcNow }));

// ----------------------------------------------------------------------------------------
// Sensor registration + media/log attachment
// ----------------------------------------------------------------------------------------
app.MapGet("/api/sensors", (TelemetryStore store) => Results.Ok(store.Devices()));

app.MapPost("/api/sensors", (RegisterSensorRequest req, TelemetryStore store) =>
{
    try { return Results.Ok(store.Register(req)); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapPost("/api/sensors/{id}/attachment", async (string id, HttpRequest request, TelemetryStore store) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "Expected a multipart form upload." });
    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "No file supplied." });

    try
    {
        var safeName = Path.GetFileName(file.FileName);
        var deviceDir = Path.Combine(uploadRoot, MacAddress.Normalise(id).Replace(':', '-'));
        Directory.CreateDirectory(deviceDir);
        var dest = Path.Combine(deviceDir, safeName);
        await using (var fs = File.Create(dest))
            await file.CopyToAsync(fs);

        return Results.Ok(store.AttachMedia(id, safeName, file.Length));
    }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
}).DisableAntiforgery();

// ----------------------------------------------------------------------------------------
// Telemetry ingestion + queries
// ----------------------------------------------------------------------------------------
app.MapPost("/api/telemetry", (IngestRequest req, TelemetryStore store)
    => Results.Ok(store.Ingest(req)));

app.MapPost("/api/telemetry/batch", (IngestRequest[] batch, TelemetryStore store) =>
{
    var results = batch.Select(r => store.Ingest(r)).ToList();
    return Results.Ok(new { ingested = results.Count, anomalies = results.Count(r => r.IsAnomaly) });
});

app.MapGet("/api/telemetry", (string? device, string? metric, int limit, TelemetryStore store)
    => Results.Ok(store.Query(device, metric, limit)));

app.MapGet("/api/telemetry/anomalies", (int limit, TelemetryStore store)
    => Results.Ok(store.Anomalies(limit)));

app.MapGet("/api/snapshot", (TelemetryStore store) => Results.Ok(store.Snapshot()));

app.MapGet("/api/alerts", (int limit, TelemetryStore store) => Results.Ok(store.Alerts(limit)));

// ----------------------------------------------------------------------------------------
// Recursive topology validation + aggregation (operator overloading)
// ----------------------------------------------------------------------------------------
app.MapPost("/api/topology/validate", (TopologyValidationRequest req)
    => Results.Ok(TopologyValidator.Validate(req.Root)));

app.MapGet("/api/topology/sample", () => Results.Ok(MockSeeder.SampleTopology()));

app.MapGet("/api/aggregate", (string device, string metric, string? unit, TelemetryStore store) =>
{
    var agg = store.AggregateLoad(device, metric, unit ?? "");
    return Results.Ok(new { device, metric, aggregate = agg.Value, unit = agg.Unit });
});

// ----------------------------------------------------------------------------------------
// PART 2 - Real-Time Command Stream (queues, commands, search, timeline, insights)
// ----------------------------------------------------------------------------------------
app.MapGet("/api/stream/status", (CommandStreamEngine e) => Results.Ok(e.Status()));

app.MapGet("/api/stream/devices", (CommandStreamEngine e) => Results.Ok(e.Devices()));

// Search / filter devices by category and/or alert state (records the query for pattern analysis).
app.MapGet("/api/stream/search", (string? category, string? alertState, CommandStreamEngine e)
    => Results.Ok(e.Search(category, alertState)));

// Drain the queues: ?all=true processes everything, otherwise one packet (priority first).
app.MapPost("/api/stream/process", (bool? all, CommandStreamEngine e) =>
{
    if (all == true) return Results.Ok(new { processed = e.ProcessAll(), status = e.Status() });
    var s = e.ProcessNext();
    return Results.Ok(new { processed = s is null ? 0 : 1, sample = s, status = e.Status() });
});

// Issue a manual override command (pushed onto the undo stack).
app.MapPost("/api/commands", (DeviceCommand cmd, CommandStreamEngine e) =>
{
    try { return Results.Ok(e.IssueCommand(cmd)); }
    catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
});

// Undo the most recent command (pop the stack + revert).
app.MapPost("/api/commands/undo", (CommandStreamEngine e) =>
{
    var undone = e.UndoLast();
    return undone is null ? Results.Ok(new { undone = (CommandRecord?)null, message = "Nothing to undo." })
                          : Results.Ok(new { undone });
});

app.MapGet("/api/commands/history", (int limit, CommandStreamEngine e) => Results.Ok(e.CommandHistory(limit)));

// Timestamp-ordered timeline for one device (from the SortedDictionary).
app.MapGet("/api/stream/timeline", (string device, int limit, CommandStreamEngine e)
    => Results.Ok(e.Timeline(device, limit)));

// Unique active errors / disconnected nodes (from the HashSet).
app.MapGet("/api/stream/errors", (CommandStreamEngine e) => Results.Ok(e.ActiveErrors()));

// Predictive "Suggested Actions" / "Automated Insights".
app.MapGet("/api/stream/insights", (CommandStreamEngine e) => Results.Ok(e.Insights()));

// ----------------------------------------------------------------------------------------
// PART 3 - Network Topology & Mesh Routing (graph, BFS/DFS, MST, routing, AVL history)
// ----------------------------------------------------------------------------------------
app.MapGet("/api/mesh/graph", (TopologyEngine t) => Results.Ok(t.Graph()));

app.MapGet("/api/mesh/devices", (TopologyEngine t) => Results.Ok(t.Devices()));

// Traversal: ?kind=bfs|dfs & optional ?start=<id> (defaults to the gateway).
app.MapGet("/api/mesh/traverse", (string? kind, string? start, TopologyEngine t)
    => Results.Ok(t.Traverse(kind ?? "bfs", start)));

// Minimum spanning tree: ?algorithm=prim|kruskal.
app.MapGet("/api/mesh/mst", (string? algorithm, TopologyEngine t) => Results.Ok(t.Mst(algorithm)));

// Power-efficient route from a device to the gateway (through the MST).
app.MapGet("/api/mesh/route", (string device, TopologyEngine t) => Results.Ok(t.Route(device)));

// Historical range query against the device's AVL tree (?device=&from=&to=).
app.MapGet("/api/mesh/history", (string device, DateTimeOffset? from, DateTimeOffset? to, TopologyEngine t) =>
{
    var f = from ?? DateTimeOffset.UtcNow.AddHours(-1);
    var u = to ?? DateTimeOffset.UtcNow;
    return Results.Ok(t.History(new HistoryQuery(device, f, u)));
});

app.MapGet("/api/mesh/history/stats", (string device, TopologyEngine t) => Results.Ok(t.HistoryStats(device)));

app.Run();
