// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 2: Smart-X Real-Time Processing & Device Management
// File:    SmartX.Client/Services/SmartXApiClient.CommandStream.cs
// Purpose: Part 2 additions to the async API client - the Real-Time Command Stream calls
//          (status, devices, search, queue processing, commands + undo, timeline, errors,
//          predictive insights). Kept as a partial class so Part 1's client stays intact.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Make HTTP requests using HttpClient. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient [Accessed 25 September 2026].
//   - Microsoft, 2024. HttpClientJsonExtensions Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.json.httpclientjsonextensions [Accessed 25 September 2026].
//   - Microsoft, 2024. Asynchronous programming with async and await (C#). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/ [Accessed 25 September 2026].
//   - Albahari, J. and Albahari, B., 2022. C# 10 in a Nutshell: The Definitive Reference. Sebastopol: O'Reilly Media.
// ------------------------------------------------------------------------------------
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SmartX.Core;

namespace SmartX.Client.Services;

public sealed partial class SmartXApiClient
{
    public async Task<StreamStatus?> GetStreamStatusAsync()
        => await _http.GetFromJsonAsync<StreamStatus>("/api/stream/status", _json);

    public async Task<List<DeviceStateDto>> GetStreamDevicesAsync()
        => await _http.GetFromJsonAsync<List<DeviceStateDto>>("/api/stream/devices", _json) ?? new();

    public async Task<List<DeviceStateDto>> SearchDevicesAsync(string? category, string? alertState)
    {
        var url = "/api/stream/search?x=1";
        if (!string.IsNullOrWhiteSpace(category)) url += $"&category={Uri.EscapeDataString(category)}";
        if (!string.IsNullOrWhiteSpace(alertState)) url += $"&alertState={Uri.EscapeDataString(alertState)}";
        return await _http.GetFromJsonAsync<List<DeviceStateDto>>(url, _json) ?? new();
    }

    public async Task<int> ProcessStreamAsync(bool all)
    {
        var resp = await _http.PostAsync($"/api/stream/process?all={(all ? "true" : "false")}", content: null);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("processed").GetInt32();
    }

    public async Task<CommandRecord?> IssueCommandAsync(string deviceId, CommandAction action)
    {
        var resp = await _http.PostAsJsonAsync("/api/commands", new DeviceCommand(deviceId, action), _json);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(await resp.Content.ReadAsStringAsync());
        return await resp.Content.ReadFromJsonAsync<CommandRecord>(_json);
    }

    public async Task<CommandRecord?> UndoLastCommandAsync()
    {
        var resp = await _http.PostAsync("/api/commands/undo", content: null);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (doc.RootElement.TryGetProperty("undone", out var u) && u.ValueKind == System.Text.Json.JsonValueKind.Object)
            return u.Deserialize<CommandRecord>(_json);
        return null;
    }

    public async Task<List<CommandRecord>> GetCommandHistoryAsync(int limit = 20)
        => await _http.GetFromJsonAsync<List<CommandRecord>>($"/api/commands/history?limit={limit}", _json) ?? new();

    public async Task<List<TimelinePoint>> GetTimelineAsync(string deviceId, int limit = 60)
        => await _http.GetFromJsonAsync<List<TimelinePoint>>(
               $"/api/stream/timeline?device={Uri.EscapeDataString(deviceId)}&limit={limit}", _json) ?? new();

    public async Task<List<string>> GetActiveErrorsAsync()
        => await _http.GetFromJsonAsync<List<string>>("/api/stream/errors", _json) ?? new();

    public async Task<List<Insight>> GetInsightsAsync()
        => await _http.GetFromJsonAsync<List<Insight>>("/api/stream/insights", _json) ?? new();
}
