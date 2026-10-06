// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 3 (PoE): Smart-X Network Topology & Mesh Routing
// File:    SmartX.Client/Services/SmartXApiClient.Mesh.cs
// Purpose: Part 3 additions to the async API client - the mesh topology calls (graph,
//          BFS/DFS traversal, MST, routing to gateway, and AVL history range queries).
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Make HTTP requests using HttpClient. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient [Accessed 25 September 2026].
//   - Microsoft, 2024. HttpClientJsonExtensions Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.json.httpclientjsonextensions [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Net.Http;
using System.Net.Http.Json;
using SmartX.Core;

namespace SmartX.Client.Services;

public sealed partial class SmartXApiClient
{
    public async Task<MeshGraphDto?> GetMeshGraphAsync()
        => await _http.GetFromJsonAsync<MeshGraphDto>("/api/mesh/graph", _json);

    public async Task<TraversalDto?> TraverseAsync(string kind, string? start = null)
    {
        var url = $"/api/mesh/traverse?kind={Uri.EscapeDataString(kind)}";
        if (!string.IsNullOrWhiteSpace(start)) url += $"&start={Uri.EscapeDataString(start)}";
        return await _http.GetFromJsonAsync<TraversalDto>(url, _json);
    }

    public async Task<MstResult?> GetMstAsync(string algorithm = "prim")
        => await _http.GetFromJsonAsync<MstResult>($"/api/mesh/mst?algorithm={Uri.EscapeDataString(algorithm)}", _json);

    public async Task<RouteDto?> GetRouteAsync(string device)
        => await _http.GetFromJsonAsync<RouteDto>($"/api/mesh/route?device={Uri.EscapeDataString(device)}", _json);

    public async Task<List<TimelinePoint>> GetHistoryAsync(string device, DateTimeOffset from, DateTimeOffset to)
    {
        var url = $"/api/mesh/history?device={Uri.EscapeDataString(device)}" +
                  $"&from={Uri.EscapeDataString(from.ToUniversalTime().ToString("o"))}" +
                  $"&to={Uri.EscapeDataString(to.ToUniversalTime().ToString("o"))}";
        return await _http.GetFromJsonAsync<List<TimelinePoint>>(url, _json) ?? new();
    }
}
