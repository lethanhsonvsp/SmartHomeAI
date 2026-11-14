using System.Diagnostics;

namespace SmartHomeAI.Dashboard.Services;

/// <summary>Tìm gốc repo và dựng/dừng tiến trình con. Lệnh chỉ đến từ cấu hình, không bao giờ từ trình duyệt.</summary>
static class Repo
{
    public static string Root(IConfiguration c)
    {
        if (!string.IsNullOrEmpty(c["Repo:Root"])) return Path.GetFullPath(c["Repo:Root"]!);
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "SmartHomeAI.slnx"))) return d.FullName;
        return Directory.GetCurrentDirectory();
    }

    // shortcut: tách tham số theo dấu cách (không hỗ trợ đường dẫn có khoảng trắng), nâng cấp khi cần.
    public static ProcessStartInfo Psi(string file, string args, string dir, bool redirect)
    {
        var psi = new ProcessStartInfo(file) { WorkingDirectory = dir, UseShellExecute = false, RedirectStandardOutput = redirect, RedirectStandardError = redirect };
        foreach (var a in args.Split(' ', StringSplitOptions.RemoveEmptyEntries)) psi.ArgumentList.Add(a);
        return psi;
    }

    public static bool PortOpen(string host, int port, int ms = 700)
    {
        try { using var t = new System.Net.Sockets.TcpClient(); return t.ConnectAsync(host, port).Wait(ms) && t.Connected; }
        catch (Exception) { return false; }
    }

    /// <summary>SIGTERM trước để nhả webcam/cổng, sau vài giây mới kill cả cây tiến trình.</summary>
    public static void Stop(Process p, int graceMs)
    {
        try
        {
            if (p.HasExited) return;
            using (var k = Process.Start("kill", ["-TERM", p.Id.ToString()])) k?.WaitForExit(1000);
            if (!p.WaitForExit(graceMs)) p.Kill(true);
            p.WaitForExit(2000);
        }
        catch (Exception) { }
    }
}
