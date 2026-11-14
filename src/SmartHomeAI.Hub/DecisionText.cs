using System.Globalization;
using SmartHomeAI.Core.Automation;
using SmartHomeAI.Core.Occupancy;

namespace SmartHomeAI.Hub;

/// <summary>Vietnamese wording of decisions; decimals use a comma.</summary>
public static class DecisionText
{
    public static string DeviceName(string id) => id switch { "light" => "đèn", "fan" => "quạt trần", _ => id };
    public static string Zone(string? z) => z switch
    {
        "livingroom" => "phòng khách", "sofa" => "sofa", "door" => "cửa", "other_exit" => "lối sang khu khác", _ => "không rõ"
    };
    public static string Posture(string? p) => p switch
    {
        "standing" => "đứng", "sitting" => "ngồi", "lying" => "nằm", "moving" => "đi lại", _ => "chưa rõ"
    };
    public static string State(OccupancyState s) => s switch
    {
        OccupancyState.Occupied => "Có người",
        OccupancyState.OtherArea => "Người ở khu vực khác",
        OccupancyState.ExitPending => "Đang chờ xác nhận rời phòng",
        OccupancyState.AwayConfirmed => "Đã rời phòng",
        _ => "Chưa rõ (mất dữ liệu)",
    };

    static string N(object? v, string fmt = "0.#") => Convert.ToDouble(v, CultureInfo.InvariantCulture).ToString(fmt, CultureInfo.InvariantCulture).Replace('.', ',');
    public static string Cap(string s) => char.ToUpper(s[0]) + s[1..];
    static string Minutes(object? seconds) => Convert.ToDouble(seconds, CultureInfo.InvariantCulture) is var s && s >= 90 ? $"{Math.Ceiling(s / 60)} phút" : $"{s} giây";

    public static string TurnOn(Diagnosis d) => d.DeviceId == "light"
        ? $"Bật đèn: phòng tối ({N(d.Params["lux"])} lux < {N(d.Params["threshold"])})"
        : $"Bật quạt trần: có người nằm ở sofa và nóng ({N(d.Params["temp"])} °C ≥ {N(d.Params["threshold"])} °C)";

    public static string Wait(Diagnosis d)
    {
        var name = Cap(DeviceName(d.DeviceId));
        if (d.Code == DiagnosisCode.StartingUp) return "Đang chờ thiết bị báo trạng thái sau khi khởi động";
        if (d.Code == DiagnosisCode.DeviceUnresponsive) return $"{name} không phản hồi sau {d.Params["count"]} lần: dừng gửi lệnh tự động cho tới khi thiết bị báo trạng thái";
        if (d.Code == DiagnosisCode.AutoLocked) return $"{name} bị khóa tự động 1 giờ: đã đổi {d.Params["count"]} lần trong 1 giờ (nghi bật tắt liên tục)";
        var why = d.Code switch
        {
            DiagnosisCode.LyingNotLongEnough => $"mới nằm ở sofa {N(d.Params["seconds"])} s, chờ đủ {N(d.Params["need"])} s",
            DiagnosisCode.NotOccupied => $"hiện diện đang là \"{State((OccupancyState)d.Params["state"]!)}\"",
            DiagnosisCode.LightBrightEnough => $"phòng đủ sáng ({N(d.Params["lux"])} lux ≥ {N(d.Params["threshold"])})",
            DiagnosisCode.NoLuxData => "chưa có dữ liệu độ sáng",
            DiagnosisCode.FanNotOnSofa => d.Params["zone"] is string z
                ? $"người chưa ở vùng sofa (đang ở: {Zone(z)}, tư thế: {Posture(d.Params["posture"] as string)})" : "người chưa ở vùng sofa (chưa thấy ai)",
            DiagnosisCode.FanNotLying => $"người ở sofa nhưng chưa nằm (tư thế: {Posture(d.Params["posture"] as string)})",
            DiagnosisCode.FanTooCool => $"nhiệt độ {N(d.Params["temp"], "0.0")} °C chưa tới {N(d.Params["threshold"])} °C",
            DiagnosisCode.NoTempData => "chưa có dữ liệu nhiệt độ",
            DiagnosisCode.BlockedByManual => $"bạn vừa tắt tay, tự động tạm dừng còn {Minutes(d.Params["seconds"])}",
            DiagnosisCode.Cooldown => $"vừa ra lệnh bật, chờ thêm {Minutes(d.Params["seconds"])}",
            _ => d.Code.ToString(),
        };
        return $"{name} chưa bật: {why}";
    }
}
