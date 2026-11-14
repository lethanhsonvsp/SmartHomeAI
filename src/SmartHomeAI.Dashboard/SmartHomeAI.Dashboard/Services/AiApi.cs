using System.Text;

namespace SmartHomeAI.Dashboard.Services;

/// <summary>Proxy phía server tới HTTP của AI (chỉ 127.0.0.1, chỉ các đường dẫn cố định); trình duyệt không chọn được địa chỉ hay lệnh.</summary>
public class AiApi(IConfiguration cfg)
{
    readonly HttpClient _http = new() { BaseAddress = new Uri($"http://127.0.0.1:{(cfg["Ai:StreamPort"] is { Length: > 0 } p ? p : "8090")}"), Timeout = TimeSpan.FromSeconds(4) };

    public async Task<(int Status, byte[] Body, string Type)> Call(HttpMethod m, string path, byte[]? body = null)
    {
        try
        {
            using var req = new HttpRequestMessage(m, path);
            if (body is not null) req.Content = new ByteArrayContent(body) { Headers = { ContentType = new("application/json") } };
            using var r = await _http.SendAsync(req);
            return ((int)r.StatusCode, await r.Content.ReadAsByteArrayAsync(), r.Content.Headers.ContentType?.ToString() ?? "application/json");
        }
        catch (Exception e)
        {
            return (503, Encoding.UTF8.GetBytes($"{{\"error\":\"Không nối được AI: {e.Message.Replace("\"", "'")}\"}}"), "application/json");
        }
    }

    public async Task<Dictionary<string, bool>?> GetConfig()
    {
        var (s, b, _) = await Call(HttpMethod.Get, "/config");
        return s == 200 ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(b) : null;
    }

    public async Task<bool> SetConfig(string key, bool v) =>
        (await Call(HttpMethod.Post, "/config", Encoding.UTF8.GetBytes($"{{\"{key}\":{(v ? "true" : "false")}}}"))).Status == 200;

    public static void Map(WebApplication app)
    {
        async Task<IResult> Go(AiApi ai, HttpMethod m, string path, HttpRequest? rq = null)
        {
            byte[]? body = null;
            if (rq is not null)
            {
                if (rq.ContentLength > 65536) return Results.StatusCode(413);
                using var ms = new MemoryStream(); await rq.Body.CopyToAsync(ms); body = ms.ToArray();
                if (body.Length > 65536) return Results.StatusCode(413);
            }
            var (s, b, t) = await ai.Call(m, path, body);
            return Results.Bytes(b, t, null, false, null, null).Status(s);
        }
        app.MapGet("/api/ai/zones", (AiApi a) => Go(a, HttpMethod.Get, "/zones"));
        app.MapGet("/api/ai/zones/default", (AiApi a) => Go(a, HttpMethod.Get, "/zones/default"));
        app.MapPut("/api/ai/zones", (AiApi a, HttpRequest r) => Go(a, HttpMethod.Put, "/zones", r));
        app.MapGet("/api/ai/frame.jpg", async (AiApi a, HttpContext c) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            return await Go(a, HttpMethod.Get, "/frame.jpg");
        });
    }
}

static class ResultExt
{
    public static IResult Status(this IResult r, int code) => new StatusWrap(r, code);
    sealed class StatusWrap(IResult inner, int code) : IResult
    {
        public Task ExecuteAsync(HttpContext c) { c.Response.StatusCode = code; return inner.ExecuteAsync(c); }
    }
}
