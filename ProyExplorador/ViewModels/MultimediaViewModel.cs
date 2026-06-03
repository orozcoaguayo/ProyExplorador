using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProyExplorador.Models;
using ProyExplorador.Services;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TagLib;

namespace ProyExplorador.ViewModels
{
    public partial class MultimediaViewModel : ViewModelBase
    {
        private readonly IFileService _fileService;
        private readonly ILogger<MultimediaViewModel> _logger;
        private readonly DispatcherTimer _timer;

        [ObservableProperty] private string _currentFilePath = string.Empty;
        [ObservableProperty] private string _currentFileName = "Sin reproduccion";
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private double _volume = 0.75;
        [ObservableProperty] private double _position;
        [ObservableProperty] private double _duration = 1;
        [ObservableProperty] private string _positionText = "0:00";
        [ObservableProperty] private string _durationText = "0:00";
        [ObservableProperty] private int _currentIndex = -1;
        [ObservableProperty] private bool _isMuted;
        [ObservableProperty] private bool _isShuffled;
        [ObservableProperty] private BitmapImage? _albumArtwork;
        [ObservableProperty] private bool _hasAlbumArt;

        public ObservableCollection<FileItem> Playlist { get; } = new();

        public Action? PlayAction { get; set; }
        public Action? PauseAction { get; set; }
        public Action? StopAction { get; set; }
        public Action<string>? OpenFileAction { get; set; }
        public Func<TimeSpan>? GetPositionFunc { get; set; }
        public Action<TimeSpan>? SeekAction { get; set; }
        public Func<TimeSpan>? GetDurationFunc { get; set; }

        public MultimediaViewModel(IFileService fileService, ILogger<MultimediaViewModel> logger)
        {
            _fileService = fileService;
            _logger = logger;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += OnTimerTick;
        }

        [RelayCommand]
        private async Task OpenFolderAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Seleccionar archivos multimedia",
                Filter = "Multimedia|*.mp4;*.mkv;*.avi;*.mp3;*.wav;*.flac;*.ogg;*.wmv;*.mov|Todos|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() != true) return;
            Playlist.Clear();
            foreach (var file in dialog.FileNames)
            {
                var info = new System.IO.FileInfo(file);
                var ext = info.Extension.ToLowerInvariant();
                Playlist.Add(new FileItem { Name = info.Name, FullPath = file, Extension = ext, Icon = _fileService.GetFileIcon(ext, false) });
            }
            if (Playlist.Count > 0) await PlayItemAsync(Playlist[0]);
        }

        [RelayCommand]
        public async Task PlayItemAsync(FileItem item)
        {
            CurrentIndex = Playlist.IndexOf(item);
            CurrentFilePath = item.FullPath;
            CurrentFileName = item.Name;
            OpenFileAction?.Invoke(item.FullPath);
            IsPlaying = true;
            _timer.Start();

            // Cargar carátula de álbum para MP3
            LoadAlbumArtwork(item.FullPath);

            _logger.LogDebug("Playing: {Name}", item.Name);
            await Task.CompletedTask;
        }

        private void LoadAlbumArtwork(string filePath)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

                if (ext == ".mp3" || ext == ".flac" || ext == ".ogg" || ext == ".wav")
                {
                    using (var file = TagLib.File.Create(filePath))
                    {
                        if (file?.Tag?.Pictures.Length > 0)
                        {
                            var picture = file.Tag.Pictures[0];
                            using (var ms = new System.IO.MemoryStream(picture.Data.Data))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = ms;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                AlbumArtwork = bitmap;
                                HasAlbumArt = true;
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading album artwork");
            }

            // Si no hay carátula, mostrar placeholder
            AlbumArtwork = null;
            HasAlbumArt = false;
        }

        [RelayCommand]
        private void TogglePlayPause()
        {
            if (IsPlaying) { PauseAction?.Invoke(); _timer.Stop(); IsPlaying = false; }
            else { PlayAction?.Invoke(); _timer.Start(); IsPlaying = true; }
        }

        [RelayCommand]
        private async Task NextAsync()
        {
            if (Playlist.Count == 0) return;
            int next = IsShuffled ? Random.Shared.Next(Playlist.Count) : (CurrentIndex + 1) % Playlist.Count;
            await PlayItemAsync(Playlist[next]);
        }

        [RelayCommand]
        private async Task PreviousAsync()
        {
            if (Playlist.Count == 0) return;
            var prev = (CurrentIndex - 1 + Playlist.Count) % Playlist.Count;
            await PlayItemAsync(Playlist[prev]);
        }

        [RelayCommand]
        private void Stop()
        {
            StopAction?.Invoke();
            _timer.Stop();
            IsPlaying = false;
            Position = 0;
            PositionText = "0:00";
            AlbumArtwork = null;
            HasAlbumArt = false;
        }

        [RelayCommand]
        private void ToggleMute() => IsMuted = !IsMuted;

        [RelayCommand]
        private void ToggleShuffle() => IsShuffled = !IsShuffled;

        [RelayCommand]
        private void SeekTo(double seconds) => SeekAction?.Invoke(TimeSpan.FromSeconds(seconds));

        private void OnTimerTick(object? sender, EventArgs e)
        {
            try
            {
                var pos = GetPositionFunc?.Invoke() ?? TimeSpan.Zero;
                var dur = GetDurationFunc?.Invoke() ?? TimeSpan.Zero;
                if (dur.TotalSeconds > 0)
                {
                    Duration = dur.TotalSeconds;
                    Position = pos.TotalSeconds;
                    PositionText = FormatTime(pos);
                    DurationText = FormatTime(dur);
                    if (pos.TotalSeconds >= dur.TotalSeconds - 1) _ = NextAsync();
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Timer tick error"); }
        }

        private static string FormatTime(TimeSpan t)
            => t.Hours > 0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

        public override void OnNavigatedFrom()
        {
            _timer.Stop();
            StopAction?.Invoke();
            IsPlaying = false;
            base.OnNavigatedFrom();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
                PlayAction = null; PauseAction = null; StopAction = null;
                OpenFileAction = null; GetPositionFunc = null; SeekAction = null; GetDurationFunc = null;
            }
            base.Dispose(disposing);
        }
    }
}