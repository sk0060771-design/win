using System.Net;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using ReelForge.Core;

namespace ReelForge.Server;

public sealed class ApiRouter
{
    private readonly AppPaths paths;
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly RenderEngine renderer;
    private readonly Dictionary<string, string> originalMedia = new(StringComparer.OrdinalIgnoreCase);
    private JsonObject progress = new() { ["status"] = "idle", ["progress"] = 0 };
    public ApiRouter(AppPaths paths) { this.paths = paths; renderer = new RenderEngine(paths, p => progress = p, ResolveMedia); }

    public JsonObject AddOriginalMedia(string source, string kind)
    {
        var requested = Path.GetFileName(source);
        var name = requested;
        var index = 2;
        while (originalMedia.ContainsKey(name) || File.Exists(Path.Combine(paths.Media, name)))
            name = $"{Path.GetFileNameWithoutExtension(requested)} ({index++}){Path.GetExtension(requested)}";
        originalMedia[name] = Path.GetFullPath(source);
        return new JsonObject
        {
            ["name"] = name,
            ["type"] = IsAudio(name) ? "audio" : "image",
            ["kind"] = kind,
            ["duration"] = 0,
            ["date"] = new DateTimeOffset(File.GetLastWriteTimeUtc(source)).ToUnixTimeSeconds()
        };
    }

    public async void Handle(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        using var deferral = e.GetDeferral();
        var uri = new Uri(e.Request.Uri); var path = Uri.UnescapeDataString(uri.AbsolutePath);
        if (path.StartsWith("/media/") || path.StartsWith("/thumb/") || path.StartsWith("/preview/") || path.StartsWith("/exports/"))
        {
            var folder = path.StartsWith("/media/") || path.StartsWith("/thumb/") ? paths.Media : path.StartsWith("/preview/") ? paths.Cache : paths.Exports;
            var fileName = Safe(path[(path.IndexOf('/', 1) + 1)..]);
            var file = folder == paths.Media ? ResolveMedia(fileName) : Path.Combine(folder, fileName);
            if (File.Exists(file))
            {
                var stream = File.OpenRead(file);
                e.Response = ((CoreWebView2)sender!).Environment.CreateWebResourceResponse(stream, 200, "OK", $"Content-Type: {Mime(file)}\r\n");
            }
            return;
        }
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return;
        try { var (body, type, status) = await Route(path, e.Request); e.Response = Response((CoreWebView2)sender!, status, type, body); }
        catch (Exception ex) { e.Response = Response((CoreWebView2)sender!, 500, "application/json", JsonSerializer.Serialize(new { ok = false, error = ex.Message })); }
    }

    private async Task<(string body, string type, int status)> Route(string path, CoreWebView2WebResourceRequest request)
    {
        if (path == "/api/hello") return (JsonSerializer.Serialize(new { cores = Environment.ProcessorCount, font = true, media = Media(), projects = Projects() }, json), "application/json", 200);
        if (path == "/api/projects") return (JsonSerializer.Serialize(new { projects = Projects() }, json), "application/json", 200);
        if (path == "/api/progress") return (progress.ToJsonString(), "application/json", 200);
        if (path == "/api/project" && request.Method == "GET") { var n = Query(request.Uri, "name") ?? "last.json"; return (Project(n), "application/json", 200); }
        if (path == "/api/project/new") return await NewProject(request);
        if (path == "/api/project/delete") return await DeleteProject(request);
        if (path == "/api/save") return await Save(request);
        if (path == "/api/upload") return await Upload(request);
        if (path == "/api/delete") return await DeleteMedia(request);
        if (path == "/api/preview" || path == "/api/export") return await Render(request, path.EndsWith("preview"));
        if (path == "/api/cancel") { renderer.Cancel(); return ("{\"ok\":true}", "application/json", 200); }
        if (path == "/api/openfolder") { AppPaths.OpenFolder(paths.Exports); return ("{\"ok\":true}", "application/json", 200); }
        return ("{\"ok\":false,\"error\":\"not found\"}", "application/json", 404);
    }

    private string Project(string name) { var file = Path.Combine(paths.Projects, Safe(name)); return File.Exists(file) ? JsonSerializer.Serialize(new { ok = true, project = JsonNode.Parse(File.ReadAllText(file)) }, json) : JsonSerializer.Serialize(new { ok = true, project = new { } }, json); }
    private object[] Projects() => Directory.EnumerateFiles(paths.Projects, "*.json").Select(f => new { name = Path.GetFileName(f), mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(f)).ToUnixTimeSeconds(), clips = 0, voice = 0, dur = 0 }).Cast<object>().ToArray();
    private object[] Media() => Directory.EnumerateFiles(paths.Media).Select(f => new { name = Path.GetFileName(f), type = IsAudio(f) ? "audio" : "image", kind = "music", duration = 0, date = new DateTimeOffset(File.GetLastWriteTimeUtc(f)).ToUnixTimeSeconds() }).Cast<object>().Concat(originalMedia.Select(x => (object)new { name = x.Key, type = IsAudio(x.Key) ? "audio" : "image", kind = "music", duration = 0, date = new DateTimeOffset(File.GetLastWriteTimeUtc(x.Value)).ToUnixTimeSeconds() })).ToArray();
    private static bool IsAudio(string f) => new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase);
    private async Task<(string, string, int)> Save(CoreWebView2WebResourceRequest r) { var o = await Body(r); var n = Safe(o["name"]?.GetValue<string>() ?? "last.json"); File.WriteAllText(Path.Combine(paths.Projects, n), o["project"]?.ToJsonString() ?? "{}"); return ("{\"ok\":true}", "application/json", 200); }
    private async Task<(string, string, int)> NewProject(CoreWebView2WebResourceRequest r) { var o = await Body(r); var n = Safe(o["name"]?.GetValue<string>() ?? "project") + ".json"; File.WriteAllText(Path.Combine(paths.Projects, n), "{}"); return (JsonSerializer.Serialize(new { ok = true, name = n, project = new { }, projects = Projects() }, json), "application/json", 200); }
    private async Task<(string, string, int)> DeleteProject(CoreWebView2WebResourceRequest r) { var o = await Body(r); var f = Path.Combine(paths.Projects, Safe(o["name"]?.GetValue<string>() ?? "")); if (File.Exists(f)) File.Delete(f); return (JsonSerializer.Serialize(new { ok = true, projects = Projects() }, json), "application/json", 200); }
    private async Task<(string, string, int)> DeleteMedia(CoreWebView2WebResourceRequest r) { var o = await Body(r); var name = Safe(o["name"]?.GetValue<string>() ?? ""); originalMedia.Remove(name); var f = Path.Combine(paths.Media, name); if (File.Exists(f)) File.Delete(f); return ("{\"ok\":true}", "application/json", 200); }
    private async Task<(string, string, int)> Upload(CoreWebView2WebResourceRequest r)
    {
        var requested = Safe(WebUtility.UrlDecode(r.Headers.GetHeader("X-Name")) ?? "upload.bin");
        var name = UniqueMediaName(requested);
        var kind = r.Headers.GetHeader("X-Kind")?.ToLowerInvariant() switch
        {
            "sfx" => "sfx",
            "voice" => "voice",
            _ => "music"
        };
        var target = Path.Combine(paths.Media, name);
        if (r.Content is null)
            throw new InvalidOperationException("Upload body khaali hai");
        using var input = r.Content;
        using var output = File.Create(target);
        await input.CopyToAsync(output);
        var media = new { name, type = IsAudio(name) ? "audio" : "image", kind, duration = 0, date = new DateTimeOffset(File.GetLastWriteTimeUtc(target)).ToUnixTimeSeconds() };
        return (JsonSerializer.Serialize(new { ok = true, media }, json), "application/json", 200);
    }
    private async Task<(string, string, int)> Render(CoreWebView2WebResourceRequest r, bool preview) { var o = await Body(r); _ = Task.Run(() => renderer.Render(o["project"] ?? new JsonObject(), preview)); return ("{\"ok\":true}", "application/json", 200); }
    private async Task<JsonObject> Body(CoreWebView2WebResourceRequest r) { using var reader = new StreamReader(r.Content); return JsonNode.Parse(await reader.ReadToEndAsync())?.AsObject() ?? new(); }
    private static string Safe(string value) => Path.GetFileName(value.Replace('/', Path.DirectorySeparatorChar));
    private string ResolveMedia(string name) => originalMedia.TryGetValue(Safe(name), out var source) ? source : Path.Combine(paths.Media, Safe(name));
    private string UniqueMediaName(string requested)
    {
        var name = Path.GetFileNameWithoutExtension(requested);
        var ext = Path.GetExtension(requested);
        var candidate = requested;
        var index = 2;
        while (File.Exists(Path.Combine(paths.Media, candidate)))
            candidate = $"{name} ({index++}){ext}";
        return candidate;
    }
    private static string Mime(string file) => Path.GetExtension(file).ToLowerInvariant() switch { ".mp4" => "video/mp4", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".wav" => "audio/wav", ".mp3" => "audio/mpeg", _ => "application/octet-stream" };
    private static string? Query(string uri, string key) => new Uri(uri).Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2 && p[0] == key).Select(p => WebUtility.UrlDecode(p[1])).FirstOrDefault();
    private static CoreWebView2WebResourceResponse Response(CoreWebView2 webView, int status, string type, string body) => webView.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes(body)), status, status == 200 ? "OK" : "Error", $"Content-Type: {type}\r\n");
}