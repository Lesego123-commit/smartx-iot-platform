// ------------------------------------------------------------------------------------
// PROG7312 - Programming 3B | Part 1: Smart-X Data Ingestion & Validation Gateway
// File:    SmartX.Client/Services/SmartXApiClient.cs
// Purpose: Typed async HttpClient wrapper over the Smart-X gateway API.
//
// References (Harvard) - sources consulted for the techniques used in this file:
//   - Microsoft, 2024. Make HTTP requests using HttpClient. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient [Accessed 10 September 2026].
//   - Microsoft, 2024. HttpClientJsonExtensions Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.json.httpclientjsonextensions [Accessed 10 September 2026].
//   - Microsoft, 2024. MultipartFormDataContent Class. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.multipartformdatacontent [Accessed 10 September 2026].
//   - Microsoft, 2024. Asynchronous programming with async and await (C#). Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/ [Accessed 10 September 2026].
//   - Microsoft, 2024. How to serialize and deserialize JSON using System.Text.Json. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/ [Accessed 10 September 2026].
// ------------------------------------------------------------------------------------
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using SmartX.Core;

namespace SmartX.Client.Services;

/// <summary>
/// Async HttpClient wrapper over the Smart-X gateway API. All communication with the
/// backend flows through here so the UI stays responsive (no synchronous blocking).
/// DTOs are the shared records from SmartX.Core — one contract, both layers.
/// </summary>
public sealed partial class SmartXApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public string BaseAddress => _http.BaseAddress?.ToString() ?? "";

    public SmartXApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            var r = await _http.GetAsync("/api/health");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<SensorProfile>> GetSensorsAsync()
        => await _http.GetFromJsonAsync<List<SensorProfile>>("/api/sensors", _json) ?? new();

    public async Task<SensorProfile> RegisterAsync(RegisterSensorRequest req)
    {
        var resp = await _http.PostAsJsonAsync("/api/sensors", req, _json);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadError(resp));
        return (await resp.Content.ReadFromJsonAsync<SensorProfile>(_json))!;
    }

    public async Task<SensorProfile> UploadAttachmentAsync(string deviceId, string filePath)
    {
        using var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        var resp = await _http.PostAsync($"/api/sensors/{Uri.EscapeDataString(deviceId)}/attachment", form);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadError(resp));
        return (await resp.Content.ReadFromJsonAsync<SensorProfile>(_json))!;
    }

    public async Task<TelemetrySample> IngestAsync(IngestRequest req)
    {
        var resp = await _http.PostAsJsonAsync("/api/telemetry", req, _json);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TelemetrySample>(_json))!;
    }

    public async Task<List<TelemetrySample>> GetTelemetryAsync(string? device, string? metric, int limit)
    {
        var url = $"/api/telemetry?limit={limit}";
        if (!string.IsNullOrWhiteSpace(device)) url += $"&device={Uri.EscapeDataString(device)}";
        if (!string.IsNullOrWhiteSpace(metric)) url += $"&metric={Uri.EscapeDataString(metric)}";
        return await _http.GetFromJsonAsync<List<TelemetrySample>>(url, _json) ?? new();
    }

    public async Task<IngestionSnapshot?> GetSnapshotAsync()
        => await _http.GetFromJsonAsync<IngestionSnapshot>("/api/snapshot", _json);

    public async Task<TopologyValidationResult?> ValidateTopologyAsync(DeploymentNode root)
    {
        var resp = await _http.PostAsJsonAsync("/api/topology/validate",
            new TopologyValidationRequest(root), _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TopologyValidationResult>(_json);
    }

    public async Task<DeploymentNode?> GetSampleTopologyAsync()
        => await _http.GetFromJsonAsync<DeploymentNode>("/api/topology/sample", _json);

    private static async Task<string> ReadError(HttpResponseMessage resp)
    {
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("error", out var e))
                return e.GetString() ?? resp.ReasonPhrase ?? "Request failed.";
        }
        catch { /* fall through */ }
        return $"{(int)resp.StatusCode} {resp.ReasonPhrase}";
    }
}
