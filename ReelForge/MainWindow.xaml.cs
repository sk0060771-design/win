using Microsoft.Web.WebView2.Core;
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
            Editor.Source = new Uri("https://reelforge.local/editor.html");
        };
    }
}