using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using ReelForge.Server;

namespace ReelForge.Core;

public sealed class RenderEngine
{
    private readonly AppPaths paths; private readonly Action<JsonObject> report; private Process? running;
    private readonly Func<string, string> resolveMedia;
    public RenderEngine(AppPaths paths, Action<JsonObject> report, Func<string, string> resolveMedia) { this.paths = paths; this.report = report; this.resolveMedia = resolveMedia; }
    public void Cancel() { try { if (running is not null && !running.HasExited) running.Kill(true); } catch { } }
    public async Task Render(JsonNode project, bool preview)
    {
        var clips = project["clips"]?.AsArray().Select(c => c?["file"]?.GetValue<string>()).Where(f => f is not null).ToArray() ?? Array.Empty<string?>();
        if (clips.Length == 0) { report(new JsonObject { ["status"] = "error", ["message"] = "No clips" }); return; }
        var output = Path.Combine(preview ? paths.Cache : paths.Exports, $"{(preview ? "preview" : "export")}-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
        report(new JsonObject { ["status"] = "running", ["kind"] = preview ? "preview" : "export", ["progress"] = 0.1 });
        var input = resolveMedia(clips[0]!); var args = $"-y -loop 1 -i \"{input}\" -t 5 -vf \"format=yuv420p,scale=trunc(iw/2)*2:trunc(ih/2)*2\" -r 30 \"{output}\"";
        running = Process.Start(new ProcessStartInfo(paths.Ffmpeg, args) { UseShellExecute = false, CreateNoWindow = true }); if (running is not null) await running.WaitForExitAsync();
        if (File.Exists(output)) report(new JsonObject { ["status"] = "done", ["progress"] = 1, ["kind"] = preview ? "preview" : "export", ["result"] = new JsonObject { ["url"] = $"/{(preview ? "preview" : "exports")}/{Path.GetFileName(output)}", ["path"] = output, ["seconds"] = 5, ["size"] = new FileInfo(output).Length } });
        else report(new JsonObject { ["status"] = "error", ["message"] = "FFmpeg render failed" });
    }
}