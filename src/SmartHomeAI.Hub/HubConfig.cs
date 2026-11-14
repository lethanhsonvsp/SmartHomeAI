using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

public static class HubConfig
{
    /// <summary>Mục "Occupancy" của appsettings; thiếu thì dùng mặc định của OccupancyOptions.</summary>
    public static OccupancyOptions Occupancy(IConfiguration cfg) => cfg.GetSection("Occupancy").Get<OccupancyOptions>() ?? new();
}
