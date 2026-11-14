using System.Security.Cryptography;
using System.Text;

namespace SmartHomeAI.Hub;

public static class ApiKey
{
    /// <summary>Chưa cấu hình Api:Key thì cho qua (dev); có thì header X-Api-Key phải khớp, so sánh hằng thời gian.</summary>
    public static bool Allowed(string? configured, string? supplied)
    {
        if (string.IsNullOrEmpty(configured)) return true;
        // băm SHA-256 để hai vế cùng độ dài, không lộ độ dài khóa
        return supplied is not null && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
    }

    public static EndpointFilterDelegate Filter(EndpointFilterFactoryContext _, EndpointFilterDelegate next) => async ctx =>
    {
        var key = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Api:Key"];
        return Allowed(key, ctx.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault())
            ? await next(ctx) : Results.Json(new { error = "invalid or missing X-Api-Key" }, statusCode: 401);
    };
}
