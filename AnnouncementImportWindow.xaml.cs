using System.Windows;
using System.Windows.Media.Imaging;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

namespace MuralInstitucional;

public partial class AnnouncementImportWindow : Window
{
    private readonly BoardViewModel board;
    private readonly AnnouncementImportPreview preview;
    private readonly bool duplicate;
    public AnnouncementImportWindow(BoardViewModel board, AnnouncementImportPreview preview)
    {
        InitializeComponent(); this.board = board; this.preview = preview;
        DataContext = preview.Announcement;
        duplicate = AnnouncementExchangeService.IsDuplicate(board.Announcements, preview);
        DuplicateNotice.Text = duplicate ? "Este anúncio já está neste mural. Você pode cancelar ou criar uma cópia independente, sem sobrescrever o cadastro existente." : "";
        ConfirmButton.Content = duplicate ? "Criar cópia independente" : "Confirmar importação";
        var item = preview.Announcement;
        PlayVideo.Visibility = item.MediaPath is not null && item.MediaKind == MediaKind.Video ? Visibility.Visible : Visibility.Collapsed;
        PreviewVideo.Visibility = PlayVideo.Visibility;
        MediaNotice.Text = item.MediaPath is null ? "Sem mídia física no pacote." : item.MediaKind == MediaKind.Video ? "Vídeo validado. Reprodução manual; depende dos codecs do Windows." : "Imagem do pacote.";
        if (item.MediaPath is not null && item.MediaKind == MediaKind.Image)
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 440; bitmap.UriSource = new Uri(item.MediaPath); bitmap.EndInit(); bitmap.Freeze();
            PreviewImage.Source = bitmap;
        }
        Closed += (_, _) => { PreviewVideo.Close(); PreviewVideo.Source = null; };
    }
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        PreviewVideo.Source ??= new Uri(preview.Announcement.MediaPath!);
        PreviewVideo.Position = TimeSpan.Zero; PreviewVideo.Play();
    }
    private void Video_Failed(object sender, ExceptionRoutedEventArgs e) => MediaNotice.Text = "Prévia indisponível: codec não suportado pelo Windows.";
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PreviewVideo.Close(); PreviewVideo.Source = null;
            board.ImportAnnouncement(preview, duplicate);
            DialogResult = true;
        }
        catch (Exception ex) when (ExchangeUi.IsExpected(ex)) { ErrorText.Text = "Não foi possível importar: " + ex.Message; }
    }
}

internal static class ExchangeUi
{
    public static bool IsExpected(Exception ex) => ex is System.IO.IOException or System.IO.InvalidDataException
        or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException
        or System.Text.Json.JsonException or System.Security.SecurityException or System.Runtime.InteropServices.COMException;
}
