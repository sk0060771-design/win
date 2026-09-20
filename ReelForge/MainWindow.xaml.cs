using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Text.Json;
using System.IO;
using System.Windows;
using ReelForge.Server;

namespace ReelForge;

public partial class MainWindow : Window
{
    private readonly AppPaths paths = new();
    private ApiRouter? router;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            router = new ApiRouter(paths);
            await Editor.EnsureCoreWebView2Async();
            Editor.CoreWebView2.SetVirtualHostNameToFolderMapping("reelforge.local", paths.WebRoot, CoreWebView2HostResourceAccessKind.Allow);
            Editor.CoreWebView2.AddWebResourceRequestedFilter("https://reelforge.local/*", CoreWebView2WebResourceContext.All);
            Editor.CoreWebView2.WebResourceRequested += router.Handle;
            Editor.CoreWebView2.WebMessageReceived += PickOriginalMedia;
            Editor.Source = new Uri("https://reelforge.local/editor.html");
        };
    }

    private void PickOriginalMedia(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var request = JsonSerializer.Deserialize<PickRequest>(e.WebMessageAsJson);
        if (request?.Type != "pick-media" || router is null) return;
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = request.Kind == "image" ? "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*" : "Audio|*.mp3;*.wav;*.m4a;*.aac;*.ogg|All files|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        var media = dialog.FileNames.Select(path => router.AddOriginalMedia(path, request.Kind)).ToArray();
        Editor.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "media-added", media }));
    }

    private sealed record PickRequest(string Type, string Kind);
}