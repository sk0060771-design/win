using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;

namespace ReelForge;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<LocalMedia> media = new();
    private readonly ObservableCollection<LocalMedia> timeline = new();

    public MainWindow()
    {
        InitializeComponent();
        MediaList.ItemsSource = media;
        TimelineList.ItemsSource = timeline;
    }

    private void AddPhotos_Click(object sender, RoutedEventArgs e)
    {
        AddFiles("Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|All files|*.*", false);
    }

    private void AddAudio_Click(object sender, RoutedEventArgs e)
    {
        AddFiles("Audio|*.mp3;*.wav;*.m4a;*.aac;*.ogg|All files|*.*", true);
    }

    private void AddFiles(string filter, bool audio)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = filter };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in dialog.FileNames)
        {
            var item = new LocalMedia(path, audio);
            media.Add(item);
            timeline.Add(item);
        }
        StatusText.Text = $"Added {dialog.FileNames.Length} local file(s) • no upload or copy";
    }

    private void MediaList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MediaList.SelectedItem is not LocalMedia item) return;
        EmptyText.Visibility = Visibility.Collapsed;
        ImagePreview.Visibility = item.IsAudio ? Visibility.Collapsed : Visibility.Visible;
        AudioPreview.Visibility = item.IsAudio ? Visibility.Visible : Visibility.Collapsed;
        if (item.IsAudio)
        {
            AudioPreview.Source = new Uri(item.Path);
            AudioPreview.Play();
        }
        else
        {
            AudioPreview.Stop();
            ImagePreview.Source = new BitmapImage(new Uri(item.Path));
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "ReelForge project|*.json", FileName = "reelforge-project.json" };
        if (dialog.ShowDialog() != true) return;
        var json = System.Text.Json.JsonSerializer.Serialize(timeline.Select(x => new { x.Name, x.Path, x.IsAudio }));
        File.WriteAllText(dialog.FileName, json);
        StatusText.Text = "Project saved locally: " + dialog.FileName;
    }

    private sealed class LocalMedia
    {
        public string Name { get; }
        public string Path { get; }
        public bool IsAudio { get; }
        public LocalMedia(string path, bool isAudio) { Path = path; Name = System.IO.Path.GetFileName(path); IsAudio = isAudio; }
    }
}