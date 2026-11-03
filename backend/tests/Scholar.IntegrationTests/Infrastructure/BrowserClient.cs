using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scholar.IntegrationTests.Infrastructure;

/// <summary>Behaves like the web app: keeps cookies, fetches a CSRF token and echoes it on unsafe requests.</summary>
public sealed class BrowserClient(HttpClient http, CookieContainer cookies) : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public HttpClient Http { get; } = http;

    public string? Cookie(string name, string path = "/api/auth") =>
        cookies.GetCookies(new Uri($"http://localhost{path}"))[name]?.Value;

    public string? CsrfToken => Cookie("ss_csrf", "/");

    public async Task EnsureCsrfAsync()
    {
        if (CsrfToken is null)
        {
            (await Http.GetAsync("/api/auth/csrf")).EnsureSuccessStatusCode();
        }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T body, bool withCsrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json) };
        if (withCsrf)
        {
            await EnsureCsrfAsync();
            request.Headers.Add("X-CSRF-Token", CsrfToken);
        }

        return await Http.SendAsync(request);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool withCsrf = true)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (withCsrf && method != HttpMethod.Get)
        {
            await EnsureCsrfAsync();
            request.Headers.Add("X-CSRF-Token", CsrfToken);
        }

        return await Http.SendAsync(request);
    }

    public Task<HttpResponseMessage> GetAsync(string url) => Http.GetAsync(url);

    public async Task<T> GetJsonAsync<T>(string url)
    {
        var response = await Http.GetAsync(url);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task LoginAsync(string identifier, string password, string? schoolCode = null)
    {
        var response = await PostAsync("/api/auth/login", new { identifier, password, schoolCode });
        await response.EnsureSuccessWithBodyAsync();
    }

    public void Dispose() => Http.Dispose();
}

public static class HttpResponseExtensions
{
    public static async Task EnsureSuccessWithBodyAsync(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<T>(BrowserClient.Json))!;
    }
}
