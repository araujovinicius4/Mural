using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

namespace MuralInstitucional.Controls;

public partial class AnnouncementCard : UserControl
{
    private Announcement? announcement;
    private readonly VideoPlaybackController playback;
    private bool opened;
    private BoardViewModel? Board => (Window.GetWindow(this) as MainWindow)?.Board;
    public AnnouncementCard()
    {
        InitializeComponent();
        playback = new VideoPlaybackController(Video.Play, Video.Pause,
            () => { Video.Stop(); Video.Position = TimeSpan.Zero; }, Video.Close);
        playback.StateChanged += UpdateVideoButtons;
        UpdateVideoButtons();
        DataContextChanged += (_, _) =>
        {
            if (announcement is not null) announcement.PropertyChanged -= Announcement_Changed;
            announcement = DataContext as Announcement;
            if (announcement is not null) announcement.PropertyChanged += Announcement_Changed;
            RefreshMedia();
        };
        Unloaded += (_, _) => StopMedia();
        Loaded += (_, _) => RefreshMedia();
    }
    private void Announcement_Changed(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(Announcement.MediaPath)) RefreshMedia(); }
    private void RefreshMedia()
    {
        StopMedia();
        playback.SetAvailable(false);
        Video.Source = null;
        Poster.Source = null;
        if (announcement is null) return;
        bool video = announcement.MediaKind == MediaKind.Video;
        Video.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        VideoActions.Visibility = video ? Visibility.Visible : Visibility.Collapsed;
        LinkSurface.Visibility = announcement.HasExternalUrl ? Visibility.Visible : Visibility.Collapsed;
        LinkBadge.Visibility = LinkSurface.Visibility;
        MediaMessage.Visibility = Visibility.Visible;
        MediaMessage.Text = video ? "Selecione um vídeo local • reprodução manual" : "Imagem indisponível";
        try
        {
            if (!video && announcement.MediaPath?.StartsWith("demo:", StringComparison.Ordinal) == true)
            {
                Poster.Source = DemoPoster(announcement.Title, announcement.Id.GetHashCode());
                MediaMessage.Visibility = Visibility.Collapsed;
            }
            else if (announcement.MediaPath is { } path && Path.IsPathFullyQualified(path) && File.Exists(path))
            {
                if (video)
                {
                    // Source is assigned only on explicit Play: loading a board never starts decoding/audio.
                    playback.SetAvailable(true);
                    MediaMessage.Text = "Vídeo local • pressione Reproduzir";
                }
                else
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 900; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                    Poster.Source = bitmap; MediaMessage.Visibility = Visibility.Collapsed;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { MediaMessage.Text = "Não foi possível carregar a mídia."; Board?.Notify($"Falha de mídia: {ex.Message}"); }
    }
    // Local, vector-based demo artwork: no downloads, image libraries or third-party assets.
    private static ImageSource DemoPoster(string title, int seed)
    {
        Color[] colors = [Color.FromRgb(36, 77, 105), Color.FromRgb(43, 112, 102), Color.FromRgb(137, 76, 86), Color.FromRgb(100, 85, 148)];
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, 600, 600)) };
        using (var dc = group.Open())
        {
            var color = colors[(int)((uint)seed % (uint)colors.Length)];
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 600, 600));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), null, new Point(490, 80), 230, 230);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), null, new Point(30, 580), 300, 250);
            var text = new FormattedText(title, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 52, Brushes.White, 1) { MaxTextWidth = 450, MaxTextHeight = 320 };
            dc.DrawText(text, new Point(64, 215));
            var label = new FormattedText("MURAL  /  DEMONSTRAÇÃO", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 20, Brushes.White, 1);
            dc.DrawText(label, new Point(64, 65));
        }
        group.Freeze();
        return new DrawingImage(group);
    }
    private void CooperationInterest_Click(object sender, RoutedEventArgs e)
    { if (announcement is not null) Board?.ToggleCooperationInterest(announcement); }
    private void Like_Click(object sender, RoutedEventArgs e) { if (announcement is not null) Board?.ToggleLike(announcement); }
    private void Comments_Click(object sender, RoutedEventArgs e)
    {
        if (announcement is not null && Board is { } board)
            new CommentsWindow(announcement, board) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
    private void Link_Click(object sender, RoutedEventArgs e)
    {
        try { ExternalLinkService.Open(announcement?.ExternalUrl); }
        catch (Exception ex) when (ex is ArgumentException or Win32Exception or InvalidOperationException)
        { Board?.Notify($"Não foi possível abrir o link: {ex.Message}"); }
    }
    private void ChooseVideo_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Escolher vídeo local demonstrativo", CheckFileExists = true,
            Filter = "Vídeos|*.mp4;*.wmv;*.avi|Todos os arquivos|*.*" };
        if (picker.ShowDialog(Window.GetWindow(this)) == true && announcement is not null)
            SelectVideo(picker.FileName);
    }
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        PlayVideo(restart: false);
    }
    private void Pause_Click(object sender, RoutedEventArgs e) => PauseMedia();
    private void Restart_Click(object sender, RoutedEventArgs e) => PlayVideo(restart: true);
    private void DemoVideo_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi");
        if (!File.Exists(path)) { Board?.Notify("Vídeo demonstrativo ausente. Use Escolher vídeo para selecionar um arquivo local."); return; }
        if (announcement is not null) SelectVideo(path);
    }
    private void PlayVideo(bool restart)
    {
        if (restart ? !playback.CanRestart : !playback.CanPlay) return;
        try
        {
            if (Video.Source is null)
            {
                if (announcement?.MediaPath is not { } path || !Path.IsPathFullyQualified(path) || !File.Exists(path))
                    throw new IOException("O arquivo de vídeo não está disponível.");
                Video.Source = new Uri(path);
            }
            MediaMessage.Text = "Carregando vídeo…";
            MediaMessage.Visibility = opened ? Visibility.Collapsed : Visibility.Visible;
            if (restart) playback.Restart();
            else playback.Play();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { FailVideo(ex.Message); }
    }
    private void Video_Opened(object sender, RoutedEventArgs e)
    {
        if (!Video.HasVideo) { FailVideo("O arquivo não contém uma faixa de vídeo reproduzível."); return; }
        opened = true; MediaMessage.Visibility = Visibility.Collapsed;
        MediaSquare.InvalidateArrange();
    }
    private void Video_Failed(object sender, ExceptionRoutedEventArgs e) => FailVideo(e.ErrorException.Message);
    private void FailVideo(string message)
    {
        StopMedia();
        playback.SetAvailable(false);
        Video.Source = null;
        MediaMessage.Visibility = Visibility.Visible;
        MediaMessage.Text = "Vídeo indisponível ou codec não suportado. Escolha outro arquivo.";
        Board?.Notify($"Falha de reprodução: {message}");
    }
    private void Video_Ended(object sender, RoutedEventArgs e)
    {
        try { playback.Ended(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { FailVideo(ex.Message); }
    }
    public void PauseMedia()
    {
        try { playback.Pause(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { FailVideo(ex.Message); }
    }
    public void StopMedia()
    {
        opened = false;
        playback.Stop();
    }
    public void PauseIfOutside(ScrollViewer viewport)
    {
        if (playback.State != VideoPlaybackState.Playing || !IsLoaded) return;
        try
        {
            var bounds = MediaSquare.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), MediaSquare.RenderSize));
            if (!bounds.IntersectsWith(new Rect(0, 0, viewport.ViewportWidth, viewport.ViewportHeight))) PauseMedia();
        }
        catch (InvalidOperationException) { PauseMedia(); }
    }
    private void UpdateVideoButtons()
    {
        PlayButton.IsEnabled = playback.CanPlay;
        PauseButton.IsEnabled = playback.CanPause;
        RestartButton.IsEnabled = playback.CanRestart;
    }
    private void ReleaseForManagement() { StopMedia(); Video.Source = null; }
    private void SelectVideo(string path)
    {
        try { ReleaseForManagement(); if (announcement is not null) Board?.SetVideo(announcement, path); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        { Board?.Notify("Não foi possível alterar o vídeo: " + ex.Message); }
    }
    private void Options_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.IsOpen = true; }
    }
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (announcement is null || Board is not { } board) return;
        ReleaseForManagement();
        new AnnouncementEditorWindow(board, announcement) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (announcement is null || Board is not { } board) return;
        if (MessageBox.Show(Window.GetWindow(this), $"Excluir ‘{announcement.Title}’ deste perfil? Curtidas e comentários associados serão removidos.",
            "Excluir anúncio", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { ReleaseForManagement(); board.DeleteAnnouncement(announcement); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { board.Notify("Não foi possível excluir: " + ex.Message); }
    }
}
