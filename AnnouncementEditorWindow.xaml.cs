using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

namespace MuralInstitucional;

public partial class AnnouncementEditorWindow : Window
{
    private readonly BoardViewModel board;
    private readonly Announcement? original;
    private string selectedPath = "";
    public AnnouncementEditorWindow(BoardViewModel board, Announcement? original = null)
    {
        InitializeComponent(); this.board = board; this.original = original;
        TypeInput.ItemsSource = AnnouncementTypes.Choices;
        TypeInput.SelectedValue = original?.Type;
        if (original is not null)
        {
            Heading.Text = original.IsExample ? "Editar exemplo demonstrativo" : "Editar anúncio";
            TitleInput.Text = original.Title; DescriptionInput.Text = original.Description;
            KindInput.SelectedIndex = original.MediaKind == MediaKind.Video ? 1 : 0;
            UrlInput.Text = original.ExternalUrl;
            selectedPath = original.MediaPath ?? "";
            UpdatePreview();
        }
    }
    private MediaKind Kind => KindInput.SelectedIndex == 1 ? MediaKind.Video : MediaKind.Image;
    private void Kind_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Preview is null) return; // XAML initialization selects the first item.
        selectedPath = ""; UpdatePreview();
    }
    private void Select_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { CheckFileExists = true, Filter = Kind == MediaKind.Image ? "Imagens|*.png;*.jpg;*.jpeg" : "Vídeos|*.avi;*.mp4;*.wmv" };
        if (dialog.ShowDialog(this) == true) { selectedPath = dialog.FileName; UpdatePreview(); }
    }
    private void UpdatePreview()
    {
        Preview.Source = null; PreviewNotice.Visibility = Visibility.Visible; FileLabel.Text = selectedPath;
        PreviewNotice.Text = Kind == MediaKind.Video ? "Vídeo selecionado. Reprodução somente no mural." : "Selecione uma imagem local.";
        try
        {
            ManagedMediaService.Validate(selectedPath, Kind);
            if (Kind == MediaKind.Image)
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 400; bitmap.UriSource = new Uri(selectedPath); bitmap.EndInit(); bitmap.Freeze();
                Preview.Source = bitmap; PreviewNotice.Visibility = Visibility.Collapsed;
            }
            ErrorText.Text = "";
        }
        catch (Exception ex) when (IsExpected(ex)) { ErrorText.Text = "Selecione uma mídia válida: " + ex.Message; }
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            board.SaveAnnouncement(original, new AnnouncementDraft(TitleInput.Text, DescriptionInput.Text, Kind, selectedPath, UrlInput.Text, TypeInput.SelectedValue is AnnouncementType type ? type : null));
            DialogResult = true;
        }
        catch (Exception ex) when (IsExpected(ex)) { ErrorText.Text = "Não foi possível salvar: " + ex.Message; }
    }
    private static bool IsExpected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException
        or NotSupportedException or InvalidOperationException or System.Security.SecurityException or System.Runtime.InteropServices.COMException;
}
