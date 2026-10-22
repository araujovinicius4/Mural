using System.IO;
using System.IO.Compression;
using System.Text.Json;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;
using MuralInstitucional;

internal static class ExchangeChecks
{
    public static void Run(string root, Action<bool, string> check)
    {
        string folder = Path.Combine(root, "exchange"); Directory.CreateDirectory(folder);
        var media = new ManagedMediaService(Path.Combine(folder, "media"));
        var users = new AnnouncementRepository(Path.Combine(folder, "users"), "local-user");
        var board = new BoardViewModel(new AnnouncementRepository(Path.Combine(folder, "demo")), users, media);
        string zip = Path.Combine(folder, "announcement.zip");
        string image = Path.Combine(folder, "source.png");
        File.Copy(Path.Combine(root, "source.png.moved"), image);
        string video = Path.Combine(AppContext.BaseDirectory, "Assets", "demo-mural.avi");
        var distribution = new AnnouncementDistribution { Kind = DistributionKind.TopicsOrSkills, Criteria = ["Pesquisa", "C#"] };
        Announcement New(string? path, MediaKind kind) => new() { Id = Guid.NewGuid(), IsExample = false, Title = "Intercâmbio",
            Description = "Descrição pública", Type = AnnouncementType.Oferta, ExternalUrl = "https://example.org",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-20), Distribution = distribution, MediaKind = kind, MediaPath = path,
            InitialLikes = 77, LikedLocally = true, Comments = [new DemoComment { Text = "SEGREDO" }] };
        foreach (var (path, kind) in new[] { (image, MediaKind.Image), (video, MediaKind.Video), ((string?)null, MediaKind.Image) })
        {
            var source = New(path, kind);
            CooperationInterestService.Register(source);
            AnnouncementExchangeService.Export(source, zip);
            using (var archive = ZipFile.OpenRead(zip))
            {
                using var reader = new StreamReader(archive.GetEntry("announcement.json")!.Open());
                var json = reader.ReadToEnd();
                check(!json.Contains("SEGREDO") && !json.Contains("Likes") && !json.Contains("Cooperation") && !json.Contains("MediaPath")
                    && (path is null || !json.Contains(path)), "ZIP contém somente dados públicos, sem caminhos ou interações privadas");
            }
            using var preview = AnnouncementExchangeService.Read(zip);
            check(board.Announcements.All(x => x.ExchangeOriginId != source.Id), "Prévia não modifica o cadastro");
            var window = new AnnouncementImportWindow(board, preview);
            check(window.DataContext == preview.Announcement && !window.IsVisible,
                "Janela de prévia instancia sem importar ou reproduzir automaticamente");
            window.Close();
            var imported = board.ImportAnnouncement(preview);
            check(imported.Id != source.Id && imported.ExchangeOriginId == source.Id && imported.IsImported && !imported.IsExample
                && imported.PublishedAt == source.PublishedAt && imported.Title == source.Title && imported.Description == source.Description
                && imported.Type == source.Type && imported.ExternalUrl == source.ExternalUrl && imported.Distribution.Criteria.SequenceEqual(distribution.Criteria)
                && imported.CooperationRequests.Count == 0 && imported.CooperationInterest is null && imported.Comments.Count == 0 && imported.LikeCount == 0,
                "Importação preserva dados públicos e distribuição com novo ID e sem dados privados");
            check(path is null ? imported.MediaPath is null : media.Owns(imported.MediaPath) && File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(imported.MediaPath!)),
                "Imagem, vídeo ou ausência de mídia preservados pelo intercâmbio");
            var bytes = File.ReadAllBytes(users.DataPath);
            bool duplicate = false;
            try { board.ImportAnnouncement(preview); } catch (InvalidOperationException) { duplicate = true; }
            check(duplicate && bytes.SequenceEqual(File.ReadAllBytes(users.DataPath)), "Duplicidade bloqueada sem sobrescrever JSON");
            var copy = board.ImportAnnouncement(preview, true);
            check(copy.Id != imported.Id && copy.ExchangeOriginId == imported.ExchangeOriginId
                && board.Announcements.Contains(imported), "Cópia independente recebe novo ID sem substituir o anúncio existente");
            string reexport = Path.Combine(folder, "reexport.zip"); AnnouncementExchangeService.Export(imported, reexport);
            using var again = AnnouncementExchangeService.Read(reexport);
            check(again.OriginId == source.Id, "Identificador de origem permanece estável na reexportação");
        }
        var protectedJson = File.ReadAllBytes(users.DataPath);
        var protectedFiles = Directory.GetFiles(media.DirectoryPath).ToDictionary(x => x, File.ReadAllBytes);
        void Preserved() => check(protectedJson.SequenceEqual(File.ReadAllBytes(users.DataPath))
            && protectedFiles.Count == Directory.GetFiles(media.DirectoryPath).Length
            && protectedFiles.All(x => x.Value.SequenceEqual(File.ReadAllBytes(x.Key))), "Erro preserva JSON e todas as mídias existentes");
        bool Reject(string file)
        { try { using var preview = AnnouncementExchangeService.Read(file); return false; }
          catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or ArgumentException) { return true; } }
        File.WriteAllText(zip, "ZIP corrompido"); check(Reject(zip), "ZIP corrompido rejeitado"); Preserved();
        var plain = New(null, MediaKind.Image);
        void Archive(string name, string content, int attributes = 0)
        {
            AnnouncementExchangeService.Export(plain, zip);
            using var archive = ZipFile.Open(zip, ZipArchiveMode.Update);
            archive.GetEntry(name)?.Delete();
            var entry = archive.CreateEntry(name); entry.ExternalAttributes = attributes;
            using var writer = new StreamWriter(entry.Open()); writer.Write(content);
        }
        Archive("manifest.json", JsonSerializer.Serialize(new ExchangeManifest(99, Guid.NewGuid(), DateTimeOffset.UtcNow)));
        check(Reject(zip), "Versão incompatível rejeitada");
        foreach (var name in new[] { "../evil.txt", "/evil.txt", "C:/evil.txt", "media/evil.png", "media\\evil.png", "unexpected.txt" })
        { Archive(name, "evil"); check(Reject(zip), "Caminho ou entrada maliciosa rejeitada: " + name); }
        Archive("media.png", "link", unchecked((int)0xA1FF0000)); check(Reject(zip), "Link simbólico ZIP rejeitado");
        Archive("announcement.json", "{}"); check(Reject(zip), "Metadados incompletos rejeitados");
        Archive("manifest.json", "{\"Version\":1,\"Version\":1,\"OriginId\":\"11111111-1111-1111-1111-111111111111\",\"ExportedAt\":\"2026-10-22T00:00:00Z\"}");
        check(Reject(zip), "Propriedades JSON duplicadas rejeitadas");
        Archive("manifest.json", "{\"Version\":1,\"OriginId\":\"11111111-1111-1111-1111-111111111111\",\"ExportedAt\":\"2026-10-22T00:00:00Z\",\"PrivateData\":true}");
        check(Reject(zip), "Propriedades JSON inesperadas rejeitadas");
        Archive("announcement.json", new string('x', 65537)); check(Reject(zip), "JSON acima de 64 KiB rejeitado");
        AnnouncementExchangeService.Export(plain, zip);
        var corrupt = File.ReadAllBytes(zip);
        for (int i = 0; i < corrupt.Length - 46; i++)
            if (BitConverter.ToUInt32(corrupt, i) == 0x02014b50) { corrupt[i + 16] ^= 1; break; }
        File.WriteAllBytes(zip, corrupt);
        check(Reject(zip), "CRC corrompido rejeitado mesmo com JSON legível");
        AnnouncementExchangeService.Export(plain, zip);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        { archive.CreateEntry("media.png"); archive.CreateEntry("media.jpg"); }
        check(Reject(zip), "Mais de três entradas rejeitadas antes de descomprimir");
        Archive("announcement.json", JsonSerializer.Serialize(new ExchangeAnnouncement("Título", null, AnnouncementType.Oferta,
            "file:///C:/Windows", DateTimeOffset.UtcNow, new(), MediaKind.Image, null)));
        check(Reject(zip), "URL local maliciosa rejeitada");
        AnnouncementExchangeService.Export(plain, zip);
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            using var stream = archive.CreateEntry("media.png", CompressionLevel.SmallestSize).Open();
            stream.Write(new byte[1024 * 1024]);
        }
        check(Reject(zip), "ZIP bomb com taxa de compressão excessiva rejeitado");
        Archive("announcement.json", JsonSerializer.Serialize(new ExchangeAnnouncement("Título", null, AnnouncementType.Comunicado,
            null, DateTimeOffset.UtcNow, new(), MediaKind.Image, "media.png")));
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            using var stream = archive.CreateEntry("media.png", CompressionLevel.NoCompression).Open();
            byte[] block = new byte[1024 * 1024];
            for (int i = 0; i < 21; i++) stream.Write(block);
        }
        check(Reject(zip), "Imagem descomprimida acima de 20 MiB rejeitada");
        string large = Path.Combine(folder, "large.zip");
        using (var stream = File.Create(large)) stream.SetLength(AnnouncementExchangeService.ArchiveLimit + 1);
        check(Reject(large), "ZIP comprimido acima do limite rejeitado"); File.Delete(large);
        var sourceImage = New(image, MediaKind.Image); AnnouncementExchangeService.Export(sourceImage, zip);
        using (var preview = AnnouncementExchangeService.Read(zip))
        {
            bool failed = false;
            try { AnnouncementExchangeService.Import(new FailingRepository(), media, board.Announcements, preview); } catch (IOException) { failed = true; }
            check(failed, "Falha de persistência durante importação é propagada");
        }
        Preserved();
        var reopened = new AnnouncementRepository(Path.Combine(folder, "users"), "local-user").Load(out _);
        check(reopened.Announcements.All(x => x.IsImported && x.ExchangeOriginId.HasValue && x.CooperationRequests.Count == 0),
            "Origem recebida e IDs de intercâmbio persistem ao reabrir");
        using (var preview = AnnouncementExchangeService.Read(zip))
        {
            var full = Enumerable.Range(0, 100).Select(_ => New(null, MediaKind.Image)).ToList();
            bool limited = false;
            try { AnnouncementExchangeService.Import(users, media, full, preview); } catch (InvalidDataException) { limited = true; }
            check(limited, "Limite de 100 anúncios respeitado com rollback da mídia");
        }
        Preserved();
        var stale = new AnnouncementRepository(Path.Combine(folder, "users"), "local-user"); stale.Load(out _);
        board.ToggleLike(board.Announcements.First(x => !x.IsExample));
        var latest = File.ReadAllBytes(users.DataPath);
        using (var preview = AnnouncementExchangeService.Read(zip))
        {
            bool conflict = false;
            try { AnnouncementExchangeService.Import(stale, media, board.Announcements, preview); } catch (IOException) { conflict = true; }
            check(conflict && latest.SequenceEqual(File.ReadAllBytes(users.DataPath)) && protectedFiles.Count == Directory.GetFiles(media.DirectoryPath).Length,
                "Importação concorrente rejeitada com rollback da nova mídia");
        }
        var original = board.Announcements.First(x => !x.IsExample && x.MediaPath is not null);
        board.ToggleLike(original); board.AddComment(original, "Interação local preservada");
        var edited = board.SaveAnnouncement(original, new AnnouncementDraft(original.Title + " editado", original.Description,
            original.MediaKind, original.MediaPath!, original.ExternalUrl, original.Type, original.Distribution));
        check(edited.IsImported && edited.ExchangeOriginId == original.ExchangeOriginId && edited.Id == original.Id
            && edited.Comments.Count == 1 && edited.LikedLocally, "Edição mantém origem recebida, ID e interações locais");
        string legacy = Path.Combine(folder, "legacy"); Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "announcements.json"), "{\"Version\":1,\"DataKind\":\"local-user\",\"Announcements\":[{\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Title\":\"Antigo\"}]}");
        var old = new AnnouncementRepository(legacy, "local-user").Load(out _).Announcements.Single();
        check(!old.IsImported && old.ExchangeOriginId is null && old.Type == AnnouncementType.Comunicado, "JSON antigo permanece compatível");
        string cancelDir;
        using (var preview = AnnouncementExchangeService.Read(zip)) cancelDir = Path.GetDirectoryName(preview.Announcement.MediaPath!)!;
        check(!Directory.Exists(cancelDir), "Cancelar ou encerrar prévia remove arquivos temporários");
    }
    private sealed class FailingRepository : IAnnouncementRepository
    {
        public string DataPath => "simulado";
        public DemoDocument Load(out string notice) { notice = ""; return new(); }
        public void Save(IEnumerable<Announcement> items) => throw new IOException("Falha simulada de gravação");
    }
}

