using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Messenger.Client.Services;

public sealed class ApiService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiPrefix;
    private readonly System.Net.CookieContainer _cookieContainer;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
        PropertyNameCaseInsensitive = true,
    };

    public ApiService(IConfiguration config)
    {
        // HttpClient автоматически обрабатывает cookies
        _cookieContainer = new System.Net.CookieContainer();
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookieContainer
        };
        _http = new HttpClient(handler);
        _baseUrl = config["Backend:BaseUrl"] ?? "http://127.0.0.1:8000";
        _apiPrefix = config["Backend:ApiPrefix"] ?? "/api";
    }

    public string? GetSessionId()
    {
        var uri = new Uri(_baseUrl);
        var cookies = _cookieContainer.GetCookies(uri);
        var sessionCookie = cookies["session_id"];
        return sessionCookie?.Value;
    }

    private Uri BuildUri(string path) => new($"{_baseUrl}{_apiPrefix}{path}");

    public async Task<T> Get<T>(string path)
    {
        using var resp = await _http.GetAsync(BuildUri(path));
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatApiError(body));
        return JsonSerializer.Deserialize<T>(body, JsonOpts)!;
    }

    public async Task<T> Post<T>(string path, object? payload = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(path));
        if (payload is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatApiError(body));
        return JsonSerializer.Deserialize<T>(body, JsonOpts)!;
    }

    public async Task<T> Patch<T>(string path, object payload)
    {
        using var req = new HttpRequestMessage(HttpMethod.Patch, BuildUri(path));
        req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatApiError(body));
        return JsonSerializer.Deserialize<T>(body, JsonOpts)!;
    }

    public async Task<T> Delete<T>(string path, object? payload = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, BuildUri(path));
        if (payload is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatApiError(body));
        return JsonSerializer.Deserialize<T>(body, JsonOpts)!;
    }

    public async Task<T> PostFile<T>(string path, string filePath, string fieldName = "file")
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var fileName = System.IO.Path.GetFileName(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, fieldName, fileName);

        using var resp = await _http.PostAsync(BuildUri(path), content);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(FormatApiError(body));
        return JsonSerializer.Deserialize<T>(body, JsonOpts)!;
    }

    public string GetFileUrl(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return "";
        // Убираем префикс "backend/" если есть
        var cleanPath = relativePath.Replace("backend/", "").Replace("\\", "/");
        return $"{_baseUrl}/media/{cleanPath}";
    }

    private static string FormatApiError(string body)
    {
        // FastAPI обычно возвращает {"detail": "..."} или {"detail":[{loc,msg,type}...]}
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("detail", out var detail))
                return body;

            if (detail.ValueKind == JsonValueKind.String)
                return detail.GetString() ?? body;

            if (detail.ValueKind == JsonValueKind.Array)
            {
                var lines = new List<string>();
                foreach (var item in detail.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var msg = item.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : null;
                        var loc = "";
                        if (item.TryGetProperty("loc", out var locEl) && locEl.ValueKind == JsonValueKind.Array)
                        {
                            loc = string.Join(".", locEl.EnumerateArray().Select(x => x.ToString()));
                        }

                        if (!string.IsNullOrWhiteSpace(msg))
                            lines.Add(string.IsNullOrWhiteSpace(loc) ? msg! : $"{loc}: {msg}");
                    }
                    else if (item.ValueKind == JsonValueKind.String)
                    {
                        lines.Add(item.GetString() ?? "");
                    }
                }

                var text = string.Join("\n", lines.Where(x => !string.IsNullOrWhiteSpace(x)));
                return string.IsNullOrWhiteSpace(text) ? body : text;
            }

            return body;
        }
        catch
        {
            return body;
        }
    }
}


