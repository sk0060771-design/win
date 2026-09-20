# ReelForge Native Windows App

ReelForge is a local Windows video editor shell built with C#/.NET 8 and WebView2. The existing `editor.html` is copied unchanged into `ReelForge/wwwroot/`; no Python server is used.

## Structure

```text
ReelForge.sln
ReelForge/
	ReelForge.csproj
	App.xaml, App.xaml.cs
	MainWindow.xaml, MainWindow.xaml.cs
	Core/RenderEngine.cs
	Server/AppPaths.cs, Server/ApiRouter.cs
	wwwroot/editor.html
.github/workflows/build-exe.yml
```

The app creates `media`, `projects`, `exports`, and `cache` beside the executable. Put `ffmpeg.exe` and `ffprobe.exe` beside the executable, or make them available on `PATH`. WebView2 Runtime is required on the target Windows PC.

## Build locally on Windows

```powershell
dotnet restore ReelForge.sln
dotnet publish ReelForge.sln -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Run `publish\ReelForge.exe`. Keep `wwwroot` in the publish folder because it contains the unchanged editor UI.

## GitHub build

Push the repository, open Actions, and run **Build ReelForge.exe**. The workflow produces `ReelForge-windows.zip`. A tag such as `v1.0.0` also starts the same build.

## Scope note

The native shell and local API preserve the editor's existing fetch paths and project format. The current first renderer produces a working FFmpeg MP4 from the first timeline clip; transitions, captions, overlays, audio mixing, and per-clip effects should be ported into `Core/RenderEngine.cs` as the next implementation slice.