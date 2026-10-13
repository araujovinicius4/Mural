using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MuralInstitucional.Controls;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "MuralChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        try
        {
            var repository = new AnnouncementRepository(root);
            var board = new BoardViewModel(repository);
            Check(board.Announcements.Count == 9 && File.Exists(repository.DataPath), "Arquivo ausente cria demonstração no diretório isolado");
            var item = board.Announcements[0]; int initial = item.LikeCount;
            board.ToggleLike(item); Check(item.LikeCount == initial + 1, "Curtir incrementa uma única interação local");
            board.ToggleLike(item); Check(item.LikeCount == initial, "Descurtir retorna ao contador inicial");
            board.ToggleLike(item);
            Check(!board.AddComment(item, "   ") && !board.AddComment(item, new string('a', 1001)), "Comentários vazios e longos rejeitados");
            Check(board.AddComment(item, "  Olá, mural!  "), "Comentário válido adicionado");
            var reopened = new BoardViewModel(repository);
            Check(reopened.Announcements[0].LikedLocally && reopened.Announcements[0].LikeCount == initial + 1, "Curtida persiste sem duplicação ao reabrir");
            Check(reopened.Announcements[0].Comments.Last().AnnouncementId == item.Id && reopened.Announcements[0].Comments.Last().Text == "Olá, mural!", "Comentário persiste associado ao anúncio");
            Check(reopened.Announcements[1].Comments.Count == 1, "Comentário não vaza para outro anúncio");
            string videoPath = Path.Combine(root, "demonstracao.mp4");
            board.SetVideo(board.Announcements[5], videoPath);
            Check(new BoardViewModel(repository).Announcements[5].MediaPath == videoPath, "Referência local de vídeo persiste");
            Check(ExternalLinkService.TryValidate("https://example.org/path", out _) && ExternalLinkService.TryValidate("http://example.org", out _), "HTTP e HTTPS válidos aceitos sem abrir navegador");
            foreach (string address in new[] { "file:///C:/Windows", "javascript:alert(1)", "mailto:a@b.com", "https://", "/relative", "https://user:pass@example.org" })
                Check(!ExternalLinkService.TryValidate(address, out _), "URL insegura/inválida rejeitada: " + address);
            File.WriteAllText(repository.DataPath, "{corrompido");
            var recovered = new BoardViewModel(repository);
            Check(recovered.Announcements.Count == 9 && Directory.GetFiles(root, "*.invalid-*").Length == 1, "JSON corrompido preservado e demonstração recuperada");
            File.WriteAllText(repository.DataPath, "{\"Version\":99,\"DataKind\":\"real\",\"Announcements\":[]}");
            Check(new BoardViewModel(repository).Announcements.Count == 9, "Schema desconhecido não tratado como dados reais");
            foreach (var width in new[] { 900d, 600d, 320d })
            {
                int columns = width == 900 ? 3 : width == 600 ? 2 : 1;
                var panel = new ResponsiveCardPanel();
                for (int i = 0; i < 9; i++) panel.Children.Add(new Border { Height = 100 });
                panel.Measure(new Size(width, double.PositiveInfinity));
                panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
                Check(panel.DesiredSize.Height == Math.Ceiling(9d / columns) * 100 && panel.Children[0].RenderSize.Width == width / columns,
                    $"Layout medido/arranjado: {columns} colunas em {width}px sem exceder largura");
            }
            foreach (var width in new[] { 180d, 320d, 450d })
            {
                var square = new SquareMediaPanel();
                square.Children.Add(new TextBlock { Text = new string('X', 500), TextWrapping = TextWrapping.Wrap });
                square.Measure(new Size(width, double.PositiveInfinity));
                square.Arrange(new Rect(0, 0, width, width));
                Check(square.DesiredSize == new Size(width, width) && square.RenderSize == new Size(width, width),
                    $"Área de mídia 1:1 em {width}px independente do conteúdo");
            }
            var unreadable = new FailingRepository { FailLoad = true };
            var fallback = new BoardViewModel(unreadable);
            fallback.ToggleLike(fallback.Announcements[0]);
            Check(fallback.Announcements.Count == 9 && unreadable.SaveCalls == 0, "Falha de leitura permite demonstração sem sobrescrever dados inacessíveis");
            var failingSave = new FailingRepository();
            var unsaved = new BoardViewModel(failingSave);
            unsaved.ToggleLike(unsaved.Announcements[0]);
            Check(unsaved.Announcements[0].LikedLocally && unsaved.Notice.Contains("não foram salvas"), "Falha de gravação mantém sessão e informa usuário");
            var card = new AnnouncementCard { DataContext = recovered.Announcements[0] };
            card.Measure(new Size(300, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, 300, card.DesiredSize.Height));
            var media = (SquareMediaPanel)card.FindName("MediaSquare");
            Console.WriteLine($"Card media geometry: {media.ActualWidth}x{media.ActualHeight}; clip={media.Clip is not null}");
            Check(Math.Abs(media.ActualWidth - media.ActualHeight) < 0.01 && media.ActualWidth > 0 && media.Clip is not null,
                "XAML do cartão instancia com mídia quadrada e geometria arredondada (sem janela visível)");
            card.StopMedia();
            var poster = (CoverImage)card.FindName("Poster");
            var artwork = new DrawingGroup();
            using (var dc = artwork.Open())
            {
                dc.DrawRectangle(Brushes.Green, null, new Rect(0, 0, 600, 200));
                dc.DrawRectangle(Brushes.Blue, null, new Rect(200, 0, 200, 200));
            }
            poster.Source = new DrawingImage(artwork);
            card.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            card.Measure(new Size(300, double.PositiveInfinity));
            card.Arrange(new Rect(0, 0, 300, card.DesiredSize.Height));
            card.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)media.ActualWidth, (int)media.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(media);
            byte[] pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, 2, 1, 1), pixel, 4, 0);
            Console.WriteLine($"Poster {poster.ActualWidth}x{poster.ActualHeight}, viewport {media.ActualWidth}x{media.ActualHeight}, BGRA={string.Join(',', pixel)}");
            Check(pixel[0] == 255 && pixel[1] == 0 && pixel[2] == 0 && pixel[3] == 255,
                "Renderização offscreen: imagem paisagem preenche topo do quadrado com recorte central");
            var portrait = new DrawingGroup();
            using (var dc = portrait.Open())
            {
                dc.DrawRectangle(Brushes.Green, null, new Rect(0, 0, 200, 600));
                dc.DrawRectangle(Brushes.Red, null, new Rect(0, 200, 200, 200));
            }
            poster.Source = new DrawingImage(portrait);
            poster.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            bitmap = new RenderTargetBitmap((int)media.ActualWidth, (int)media.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(media);
            bitmap.CopyPixels(new Int32Rect(2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
            Check(pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 255 && pixel[3] == 255,
                "Renderização offscreen: imagem retrato preenche lateral com recorte central");
            var video = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi"));
            Check(System.Text.Encoding.ASCII.GetString(video, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(video, 8, 4) == "AVI "
                && BitConverter.ToInt32(video, 4) == video.Length - 8, "Vídeo local incluído com cabeçalho RIFF/AVI e tamanho consistente");
            Check(BitConverter.ToInt32(video, 48) == 36 && BitConverter.ToInt32(video, 64) == 160 && BitConverter.ToInt32(video, 68) == 90,
                "Metadados AVI demonstrativo: 36 quadros, 160x90");
            var videoCard = new AnnouncementCard { DataContext = new Announcement { Id = Guid.NewGuid(), Title = "Vídeo", MediaKind = MediaKind.Video,
                MediaPath = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi") } };
            Check(((Button)videoCard.FindName("PlayButton")).IsEnabled && ((Button)videoCard.FindName("RestartButton")).IsEnabled
                && ((MediaElement)videoCard.FindName("Video")).Source is null, "Vídeo demo habilita Reproduzir/Reiniciar sem carregar ou reproduzir automaticamente");
            videoCard.StopMedia();
            Check(!((Button)videoCard.FindName("PauseButton")).IsEnabled
                && ((Button)videoCard.FindName("PlayButton")).Content.ToString() == "▶ Reproduzir",
                "Botão Pausar separado e desabilitado no cartão parado");
            double position = 0;
            int plays = 0, pauses = 0, resets = 0, closes = 0;
            var transport = new VideoPlaybackController(() => plays++, () => pauses++,
                () => { resets++; position = 0; }, () => { closes++; position = 0; });
            transport.Play(); transport.Pause(); transport.Restart();
            Check(plays == 0 && pauses == 0 && resets == 0 && !transport.CanPlay && !transport.CanPause && !transport.CanRestart,
                "Sem vídeo disponível, transportes permanecem desabilitados e não acionam player");
            transport.SetAvailable(true);
            Check(transport.State == VideoPlaybackState.Stopped && transport.CanPlay && !transport.CanPause && transport.CanRestart,
                "Parado: Reproduzir/Reiniciar habilitados, Pausar desabilitado");
            transport.Play(); transport.Play(); position = 1.5;
            Check(plays == 1 && transport.State == VideoPlaybackState.Playing && !transport.CanPlay && transport.CanPause && transport.CanRestart,
                "Reproduzindo: Pausar/Reiniciar habilitados, Reproduzir não alterna nem duplica comando");
            transport.Pause(); transport.Pause();
            Check(pauses == 1 && position == 1.5 && transport.State == VideoPlaybackState.Paused && transport.CanPlay && !transport.CanPause,
                "Pausar preserva posição e desabilita nova pausa");
            transport.Play();
            Check(plays == 2 && position == 1.5 && resets == 0, "Reproduzir retoma sem resetar posição");
            transport.Pause(); transport.Restart();
            Check(plays == 3 && resets == 1 && position == 0 && transport.State == VideoPlaybackState.Playing,
                "Reiniciar após pausa volta a zero e reproduz");
            position = 2; transport.Restart();
            Check(plays == 4 && resets == 2 && position == 0, "Reiniciar durante reprodução volta a zero e reproduz");
            transport.Ended();
            Check(transport.State == VideoPlaybackState.Stopped && transport.CanPlay && !transport.CanPause && position == 0,
                "Término retorna a parado com Reproduzir disponível");
            transport.Restart(); transport.Stop(); transport.SetAvailable(false);
            Check(closes == 1 && transport.State == VideoPlaybackState.Unavailable && !transport.CanPlay && !transport.CanPause && !transport.CanRestart,
                "Fechamento/falha indisponibiliza comandos sem playback automático");
            ManagementChecks.Run(root, Check);
            Console.WriteLine($"{checks} verificações concluídas. Sem Explorer, navegador ou reprodução de vídeo.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            var resolved = Path.GetFullPath(root);
            var parent = Path.GetDirectoryName(resolved);
            if (!string.Equals(parent?.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolved).StartsWith("MuralChecks-", StringComparison.Ordinal))
                throw new InvalidOperationException("Diretório temporário fora do limite esperado; limpeza recusada.");
            Directory.Delete(resolved, recursive: true);
        }
    }
    private sealed class FailingRepository : IAnnouncementRepository
    {
        public bool FailLoad { get; init; }
        public int SaveCalls { get; private set; }
        public string DataPath => "repositório simulado";
        public DemoDocument Load(out string notice)
        {
            notice = "Teste";
            if (FailLoad) throw new IOException("Leitura indisponível");
            return AnnouncementRepository.CreateDemo();
        }
        public void Save(IEnumerable<Announcement> announcements)
        { SaveCalls++; throw new IOException("Gravação indisponível"); }
    }
}
