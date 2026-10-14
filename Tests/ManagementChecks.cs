using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

internal static class ManagementChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string image = Path.Combine(root, "source.png");
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] {255,0,0,255, 0,255,0,255, 0,0,255,255, 255,255,255,255}, 8);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(image)) encoder.Save(stream);
        string video = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi");
        var users = new AnnouncementRepository(Path.Combine(root, "users"), "local-user");
        var media = new ManagedMediaService(Path.Combine(root, "media"));
        var demos = new AnnouncementRepository(Path.Combine(root, "demos"));
        var board = new BoardViewModel(demos, users, media);
        var draft = new AnnouncementDraft("  Primeiro anúncio  ", "Descrição", MediaKind.Image, image, "https://example.org", AnnouncementType.Demanda);
        var added = board.SaveAnnouncement(null, draft);
        check(!added.IsExample && added.Title == "Primeiro anúncio" && added.Id != Guid.Empty && media.Owns(added.MediaPath)
            && File.Exists(added.MediaPath) && added.MediaPath != image, "Cadastro de imagem copia mídia com ID único e separa dos exemplos");
        File.Move(image, image + ".moved");
        check(File.Exists(added.MediaPath), "Mover original não quebra mídia gerenciada");
        board.ToggleLike(added); board.AddComment(added, "Comentário preservado");
        var edited = board.SaveAnnouncement(added, draft with { Title = "Editado", Path = added.MediaPath!, Type = AnnouncementType.Comunicado });
        check(added.Type == AnnouncementType.Demanda && edited.Type == AnnouncementType.Comunicado && edited.Id == added.Id && edited.LikedLocally && edited.LikeCount == 1 && edited.Comments.Count == 1
            && edited.Comments[0].AnnouncementId == edited.Id && edited.MediaPath == added.MediaPath, "Edição preserva ID, curtida, comentários e arquivo sem cópia desnecessária");
        var videoItem = board.SaveAnnouncement(null, new AnnouncementDraft("Vídeo", null, MediaKind.Video, video, null, AnnouncementType.Oferta));
        check(videoItem.Type == AnnouncementType.Oferta && media.Owns(videoItem.MediaPath) && File.Exists(videoItem.MediaPath), "Cadastro de vídeo copia AVI local sem reprodução");
        var reopened = new BoardViewModel(new AnnouncementRepository(Path.Combine(root, "demos")),
            new AnnouncementRepository(Path.Combine(root, "users"), "local-user"), media);
        check(reopened.Announcements.Count(x => !x.IsExample) == 2 && reopened.Announcements.First(x => x.Id == edited.Id).Comments.Count == 1 && reopened.Announcements.First(x => x.Id == edited.Id).Type == AnnouncementType.Comunicado,
            "Cadastros e interações persistem ao reabrir, separados dos nove exemplos");
        var replaced = board.SaveAnnouncement(edited, new AnnouncementDraft("Imagem vira vídeo", null, MediaKind.Video, video, null, AnnouncementType.Oferta));
        check(replaced.Id == edited.Id && replaced.LikedLocally && replaced.Comments.Count == 1 && !File.Exists(edited.MediaPath),
            "Troca de mídia preserva interações e remove somente cópia antiga após commit");
        board.DeleteAnnouncement(replaced);
        check(!File.Exists(replaced.MediaPath) && File.Exists(video) && !board.Announcements.Any(x => x.Id == replaced.Id),
            "Exclusão remove anúncio/cópia gerenciada e preserva vídeo externo");
        media.DeleteOwned(image + ".moved");
        check(File.Exists(image + ".moved"), "Exclusão defensiva nunca remove arquivo externo");
        bool Reject(AnnouncementDraft candidate)
        { try { AnnouncementManagementService.Validate(candidate); return false; } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException) { return true; } }
        check(Reject(draft with { Title = " ", Path = image + ".moved" }), "Título vazio rejeitado");
        check(Reject(draft with { Path = "C:\\arquivo-inexistente.png" }), "Arquivo inexistente rejeitado");
        check(Reject(draft with { Kind = MediaKind.Video, Path = video, Url = "file:///C:/Windows" }), "URL não HTTP/HTTPS rejeitada sem abrir navegador");
        string falseImage = Path.Combine(root, "false.png"); File.WriteAllText(falseImage, "Não é uma imagem");
        check(Reject(draft with { Path = falseImage }), "Conteúdo incompatível com extensão rejeitado");
        check(Reject(draft with { Kind = MediaKind.Image, Path = video }), "Extensão incompatível com tipo rejeitada");
        string large = Path.Combine(root, "large.mp4");
        using (var stream = File.Create(large)) stream.SetLength(ManagedMediaService.VideoLimit + 1);
        check(Reject(draft with { Kind = MediaKind.Video, Path = large }), "Vídeo acima de 250 MB rejeitado antes da cópia");
        File.Delete(large);
        int before = Directory.GetFiles(media.DirectoryPath).Length;
        var fail = new AnnouncementManagementService(new WriteFailure(), media);
        try { fail.Save([], null, new AnnouncementDraft("Falha", null, MediaKind.Video, video, null, AnnouncementType.Oferta)); } catch (IOException) { }
        check(Directory.GetFiles(media.DirectoryPath).Length == before, "Falha de commit desfaz nova cópia de mídia");
        try { fail.Delete([videoItem], videoItem); } catch (IOException) { }
        check(File.Exists(videoItem.MediaPath), "Falha de exclusão JSON preserva mídia existente");
        var stale = new AnnouncementRepository(Path.Combine(root, "users"), "local-user"); stale.Load(out _);
        board.ToggleLike(videoItem);
        bool conflict = false; try { stale.Save([]); } catch (IOException) { conflict = true; }
        check(conflict && new AnnouncementRepository(Path.Combine(root, "users"), "local-user").Load(out _).Announcements.Count == 1,
            "Gravação desatualizada é rejeitada sem perda de anúncios");
        string brokenDirectory = Path.Combine(root, "broken"); Directory.CreateDirectory(brokenDirectory);
        string broken = Path.Combine(brokenDirectory, "announcements.json"); File.WriteAllText(broken, "{quebrado");
        var blocked = new BoardViewModel(new AnnouncementRepository(Path.Combine(root, "other-demos")),
            new AnnouncementRepository(brokenDirectory, "local-user"), media);
        check(!blocked.CanManageUsers && File.ReadAllText(broken) == "{quebrado", "JSON de cadastro corrompido preservado com gerenciamento bloqueado");
        var example = board.Announcements.First(x => x.IsExample);
        board.ToggleLike(example); board.AddComment(example, "Interação do exemplo");
        var editedExample = board.SaveAnnouncement(example, new AnnouncementDraft("Exemplo editado", null, MediaKind.Video, video, null, AnnouncementType.Oferta));
        check(editedExample.IsExample && editedExample.Id == example.Id && editedExample.LikedLocally && editedExample.Comments.Count == 2
            && new AnnouncementRepository(Path.Combine(root, "users"), "local-user").Load(out _).Announcements.Count == 1,
            "Editar exemplo preserva interações e classificação sem misturar cadastros");
        board.DeleteAnnouncement(editedExample);
        check(new AnnouncementRepository(Path.Combine(root, "demos")).Load(out _).Announcements.Count == 8 && !File.Exists(editedExample.MediaPath),
            "Excluir exemplo salva somente seu JSON e remove cópia gerenciada");
        var shared = new Announcement { Id = Guid.NewGuid(), IsExample = false, Title = "Referência compartilhada", MediaKind = MediaKind.Video,
            MediaPath = videoItem.MediaPath, PublishedAt = DateTimeOffset.Now };
        board.Announcements.Insert(0, shared); board.Save();
        board.DeleteAnnouncement(videoItem);
        check(File.Exists(shared.MediaPath), "Mídia compartilhada é preservada enquanto outro anúncio a referencia");
        board.DeleteAnnouncement(shared);
        check(!File.Exists(shared.MediaPath) && File.Exists(video), "Última referência remove apenas a mídia gerenciada");
    }
    private sealed class WriteFailure : IAnnouncementRepository
    {
        public string DataPath => "simulado";
        public DemoDocument Load(out string notice) { notice = ""; return new(); }
        public void Save(IEnumerable<Announcement> items) => throw new IOException("Falha simulada de commit");
    }
}
