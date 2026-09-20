# ReelForge Native Windows App

ReelForge includes a native C# WPF shell and a fully native C++ Win32 editor target. Both use local Windows file paths directly; there is no browser, WebView, upload server, or media copy step in the native targets.

The C++ target is under `Native/ReelForgeNative/`. It uses Win32 controls, GDI+ image rendering, Windows MCI audio playback, and native file dialogs.

## Structure

```text
ReelForge.sln
ReelForge/
	ReelForge.csproj
	App.xaml, App.xaml.cs
	MainWindow.xaml, MainWindow.xaml.cs
	Core/RenderEngine.cs
	Server/AppPaths.cs
.github/workflows/build-exe.yml
Native/ReelForgeNative/CMakeLists.txt, main.cpp
```

The C++ app reads files from their original C:, D:, E:, or other drive paths. It does not copy media into an app folder. The C# shell still uses the existing FFmpeg support; the C++ target uses Windows-native preview and playback.

## Build locally on Windows

```powershell
dotnet restore ReelForge.sln
dotnet publish ReelForge.sln -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Run `publish\ReelForge.exe` for the C# native shell. The GitHub workflow also creates `ReelForge-native-cpp-windows.zip` containing the C++ Win32 editor.

## GitHub build

Push the repository, open Actions, and run **Build ReelForge.exe**. The workflow produces `ReelForge-windows.zip`. A tag such as `v1.0.0` also starts the same build.

## Scope note

The native C++ editor currently provides local image/audio selection, image preview, audio playback, library-to-timeline editing, ratio/quality/FPS controls, and project path saving. Timeline effects, captions, overlays, audio mixing, and FFmpeg export remain the next native implementation slice.