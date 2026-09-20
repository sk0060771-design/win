using System.Diagnostics;
using System.IO;

namespace ReelForge.Server;

public sealed class AppPaths
{
    public string Root { get; } = AppContext.BaseDirectory;
    public string WebRoot => Path.Combine(Root, "wwwroot");
    public string Media => DirectoryPath("media");
    public string Projects => DirectoryPath("projects");
    public string Exports => DirectoryPath("exports");
    public string Cache => DirectoryPath("cache");
    public string Ffmpeg => FindTool("ffmpeg.exe");
    public string Ffprobe => FindTool("ffprobe.exe");

    public AppPaths() { }
    private string DirectoryPath(string name) { var p = Path.Combine(Root, name); Directory.CreateDirectory(p); return p; }
    private string FindTool(string name)
    {
        var local = Path.Combine(Root, name);
        if (File.Exists(local)) return local;
        var path = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).FirstOrDefault(p => File.Exists(Path.Combine(p, name)));
        return path is not null ? Path.Combine(path, name) : name;
    }
    public static void OpenFolder(string path) => Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
}