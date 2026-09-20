#define UNICODE
#define _UNICODE
#include <windows.h>
#include <commctrl.h>
#include <commdlg.h>
#include <gdiplus.h>
#include <mmsystem.h>
#include <shlwapi.h>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>
#include <algorithm>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "winmm.lib")
#pragma comment(lib, "shlwapi.lib")

using namespace Gdiplus;
namespace fs = std::filesystem;

struct MediaItem {
    std::wstring path;
    bool audio;
};

constexpr int ID_ADD_IMAGE = 101;
constexpr int ID_ADD_AUDIO = 102;
constexpr int ID_SAVE = 103;
constexpr int ID_LIBRARY = 104;
constexpr int ID_STATUS = 105;
constexpr int ID_TIMELINE = 106;
constexpr int ID_ADD_SELECTED = 107;
constexpr int ID_RATIO = 108;
constexpr int ID_QUALITY = 109;
constexpr int ID_FPS = 110;
constexpr int ID_PLAY = 111;

HWND g_library = nullptr;
HWND g_timeline = nullptr;
HWND g_status = nullptr;
std::vector<MediaItem> g_media;
std::wstring g_currentImage;
ULONG_PTR g_gdiplusToken = 0;

std::wstring FileName(const std::wstring& path) {
    return fs::path(path).filename().wstring();
}

bool IsAudio(const std::wstring& path) {
    auto ext = fs::path(path).extension().wstring();
    std::transform(ext.begin(), ext.end(), ext.begin(), towlower);
    return ext == L".mp3" || ext == L".wav" || ext == L".m4a" || ext == L".aac" || ext == L".ogg";
}

void SetStatus(const std::wstring& text) {
    SetWindowTextW(g_status, text.c_str());
}

void AddFile(const std::wstring& path, bool audio) {
    if (path.empty()) return;
    if (std::find_if(g_media.begin(), g_media.end(), [&](const MediaItem& item) { return item.path == path; }) != g_media.end()) return;
    g_media.push_back({path, audio});
    SendMessageW(g_library, LB_ADDSTRING, 0, reinterpret_cast<LPARAM>(FileName(path).c_str()));
    SetStatus(L"Local file added: " + path + L" (no copy, no upload)");
}

void AddSelectedToTimeline(HWND window) {
    const int index = static_cast<int>(SendMessageW(g_library, LB_GETCURSEL, 0, 0));
    if (index < 0 || index >= static_cast<int>(g_media.size())) {
        SetStatus(L"Select an image from the library first");
        return;
    }
    if (g_media[index].audio) {
        SetStatus(L"Audio is played from the library; select an image for the timeline");
        return;
    }
    SendMessageW(g_timeline, LB_ADDSTRING, 0, reinterpret_cast<LPARAM>(FileName(g_media[index].path).c_str()));
    g_currentImage = g_media[index].path;
    InvalidateRect(window, nullptr, FALSE);
    SetStatus(L"Added to timeline: " + g_media[index].path);
}

void OpenMedia(bool audio) {
    wchar_t path[32768] = {};
    OPENFILENAMEW dialog{};
    dialog.lStructSize = sizeof(dialog);
    dialog.hwndOwner = GetParent(g_library);
    dialog.lpstrFile = path;
    dialog.nMaxFile = static_cast<DWORD>(std::size(path));
    dialog.lpstrFilter = audio
        ? L"Audio files\0*.mp3;*.wav;*.m4a;*.aac;*.ogg\0All files\0*.*\0"
        : L"Image files\0*.jpg;*.jpeg;*.png;*.bmp;*.gif\0All files\0*.*\0";
    dialog.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;
    if (GetOpenFileNameW(&dialog)) AddFile(path, audio);
}

void SaveProject(HWND owner) {
    wchar_t path[MAX_PATH] = L"reelforge-project.json";
    OPENFILENAMEW dialog{};
    dialog.lStructSize = sizeof(dialog);
    dialog.hwndOwner = owner;
    dialog.lpstrFile = path;
    dialog.nMaxFile = MAX_PATH;
    dialog.lpstrFilter = L"ReelForge project\0*.json\0All files\0*.*\0";
    dialog.Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST;
    if (!GetSaveFileNameW(&dialog)) return;

    std::wofstream output(path);
    output << L"{\n  \"media\": [\n";
    for (size_t i = 0; i < g_media.size(); ++i) {
        std::wstring escaped = g_media[i].path;
        std::wstring safe;
        for (wchar_t c : escaped) safe += c == L'\\' ? L"\\\\" : std::wstring(1, c);
        output << L"    {\"path\": \"" << safe << L"\", \"audio\": " << (g_media[i].audio ? L"true" : L"false") << L"}";
        if (i + 1 < g_media.size()) output << L",";
        output << L"\n";
    }
    output << L"  ]\n}\n";
    SetStatus(L"Project saved: " + std::wstring(path));
}

void PlayAudio(const std::wstring& path) {
    mciSendStringW(L"stop reelforge_audio", nullptr, 0, nullptr);
    std::wstring command = L"open \"" + path + L"\" alias reelforge_audio";
    mciSendStringW(command.c_str(), nullptr, 0, nullptr);
    mciSendStringW(L"play reelforge_audio", nullptr, 0, nullptr);
}

void DrawImage(HDC dc, RECT area) {
    if (g_currentImage.empty()) return;
    Image image(g_currentImage.c_str());
    if (image.GetLastStatus() != Ok) return;
    const auto width = static_cast<float>(image.GetWidth());
    const auto height = static_cast<float>(image.GetHeight());
    const auto scale = std::min((area.right - area.left) / width, (area.bottom - area.top) / height);
    const int drawWidth = static_cast<int>(width * scale);
    const int drawHeight = static_cast<int>(height * scale);
    const int x = area.left + ((area.right - area.left) - drawWidth) / 2;
    const int y = area.top + ((area.bottom - area.top) - drawHeight) / 2;
    Graphics graphics(dc);
    graphics.SetInterpolationMode(InterpolationModeHighQualityBicubic);
    graphics.DrawImage(&image, x, y, drawWidth, drawHeight);
}

LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) {
    switch (message) {
    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case ID_ADD_IMAGE: OpenMedia(false); return 0;
        case ID_ADD_AUDIO: OpenMedia(true); return 0;
        case ID_SAVE: SaveProject(window); return 0;
        case ID_ADD_SELECTED: AddSelectedToTimeline(window); return 0;
        case ID_PLAY:
            if (!g_currentImage.empty()) InvalidateRect(window, nullptr, FALSE);
            return 0;
        case ID_LIBRARY:
            if (HIWORD(wParam) == LBN_SELCHANGE) {
                const int index = static_cast<int>(SendMessageW(g_library, LB_GETCURSEL, 0, 0));
                if (index >= 0 && index < static_cast<int>(g_media.size())) {
                    const auto& item = g_media[index];
                    if (item.audio) {
                        PlayAudio(item.path);
                        SetStatus(L"Playing local audio: " + item.path);
                    } else {
                        g_currentImage = item.path;
                        InvalidateRect(window, nullptr, FALSE);
                        SetStatus(L"Showing local image: " + item.path);
                    }
                }
            }
            return 0;
        }
        break;
    case WM_PAINT: {
        PAINTSTRUCT paint{};
        HDC dc = BeginPaint(window, &paint);
        RECT client{};
        GetClientRect(window, &client);
        RECT preview{300, 112, client.right - 25, client.bottom - 150};
        FillRect(dc, &preview, reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));
        DrawImage(dc, preview);
        EndPaint(window, &paint);
        return 0;
    }
    case WM_DESTROY:
        mciSendStringW(L"close reelforge_audio", nullptr, 0, nullptr);
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(window, message, wParam, lParam);
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int show) {
    INITCOMMONCONTROLSEX controls{sizeof(controls), ICC_STANDARD_CLASSES};
    InitCommonControlsEx(&controls);
    GdiplusStartupInput startupInput;
    GdiplusStartup(&g_gdiplusToken, &startupInput, nullptr);

    const wchar_t className[] = L"ReelForgeNativeWindow";
    WNDCLASSW windowClass{};
    windowClass.hInstance = instance;
    windowClass.lpfnWndProc = WindowProc;
    windowClass.lpszClassName = className;
    windowClass.hCursor = LoadCursor(nullptr, IDC_ARROW);
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    RegisterClassW(&windowClass);

    HWND window = CreateWindowExW(0, className, L"ReelForge Native C++", WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 1280, 800, nullptr, nullptr, instance, nullptr);
    if (!window) return 1;

    CreateWindowW(L"STATIC", L"ReelForge Native C++  |  local drives only", WS_VISIBLE | WS_CHILD,
        20, 15, 600, 30, window, nullptr, instance, nullptr);
    CreateWindowW(L"BUTTON", L"Add image", WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
        20, 50, 115, 30, window, reinterpret_cast<HMENU>(ID_ADD_IMAGE), instance, nullptr);
    CreateWindowW(L"BUTTON", L"Add audio", WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
        145, 50, 115, 30, window, reinterpret_cast<HMENU>(ID_ADD_AUDIO), instance, nullptr);
    CreateWindowW(L"BUTTON", L"Add selected to timeline", WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
        20, 685, 240, 30, window, reinterpret_cast<HMENU>(ID_ADD_SELECTED), instance, nullptr);
    CreateWindowW(L"BUTTON", L"Save project", WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
        145, 720, 115, 30, window, reinterpret_cast<HMENU>(ID_SAVE), instance, nullptr);
    g_library = CreateWindowW(L"LISTBOX", nullptr, WS_VISIBLE | WS_CHILD | WS_BORDER | LBS_NOTIFY | WS_VSCROLL,
        20, 90, 240, 580, window, reinterpret_cast<HMENU>(ID_LIBRARY), instance, nullptr);
    CreateWindowW(L"STATIC", L"Ratio", WS_VISIBLE | WS_CHILD, 300, 50, 55, 25, window, nullptr, instance, nullptr);
    HWND ratio = CreateWindowW(L"COMBOBOX", nullptr, WS_VISIBLE | WS_CHILD | CBS_DROPDOWNLIST,
        355, 47, 90, 160, window, reinterpret_cast<HMENU>(ID_RATIO), instance, nullptr);
    for (const wchar_t* value : {L"16:9", L"9:16", L"1:1", L"4:5"}) SendMessageW(ratio, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(value));
    SendMessageW(ratio, CB_SETCURSEL, 0, 0);
    CreateWindowW(L"STATIC", L"Quality", WS_VISIBLE | WS_CHILD, 460, 50, 60, 25, window, nullptr, instance, nullptr);
    HWND quality = CreateWindowW(L"COMBOBOX", nullptr, WS_VISIBLE | WS_CHILD | CBS_DROPDOWNLIST,
        520, 47, 95, 160, window, reinterpret_cast<HMENU>(ID_QUALITY), instance, nullptr);
    for (const wchar_t* value : {L"480p", L"720p", L"1080p", L"1440p"}) SendMessageW(quality, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(value));
    SendMessageW(quality, CB_SETCURSEL, 2, 0);
    CreateWindowW(L"STATIC", L"FPS", WS_VISIBLE | WS_CHILD, 630, 50, 35, 25, window, nullptr, instance, nullptr);
    HWND fps = CreateWindowW(L"COMBOBOX", nullptr, WS_VISIBLE | WS_CHILD | CBS_DROPDOWNLIST,
        665, 47, 75, 160, window, reinterpret_cast<HMENU>(ID_FPS), instance, nullptr);
    for (const wchar_t* value : {L"24", L"30", L"60"}) SendMessageW(fps, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(value));
    SendMessageW(fps, CB_SETCURSEL, 1, 0);
    CreateWindowW(L"BUTTON", L"Play preview", WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
        755, 47, 120, 30, window, reinterpret_cast<HMENU>(ID_PLAY), instance, nullptr);
    CreateWindowW(L"STATIC", L"Timeline", WS_VISIBLE | WS_CHILD,
        900, 50, 180, 25, window, nullptr, instance, nullptr);
    g_timeline = CreateWindowW(L"LISTBOX", nullptr, WS_VISIBLE | WS_CHILD | WS_BORDER | WS_VSCROLL,
        900, 75, 300, 110, window, reinterpret_cast<HMENU>(ID_TIMELINE), instance, nullptr);
    g_status = CreateWindowW(L"STATIC", L"Ready - files stay in their original location", WS_VISIBLE | WS_CHILD,
        300, 700, 900, 30, window, reinterpret_cast<HMENU>(ID_STATUS), instance, nullptr);

    ShowWindow(window, show);
    UpdateWindow(window);
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    GdiplusShutdown(g_gdiplusToken);
    return static_cast<int>(message.wParam);
}
