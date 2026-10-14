using System.IO;
using System.Text.Json;
using MuralInstitucional.Models;

namespace MuralInstitucional.Services;

public interface IAnnouncementRepository
{
    DemoDocument Load(out string notice);
    void Save(IEnumerable<Announcement> announcements);
    string DataPath { get; }
}

public sealed class AnnouncementRepository : IAnnouncementRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string DataPath { get; }
    private readonly string dataKind;
    private string? fingerprint;
    private static string Fingerprint(byte[] data) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
    public AnnouncementRepository(string? directory = null, string dataKind = "local-demo")
    {
        this.dataKind = dataKind;
        DataPath = Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MuralInstitucional", "Demo"), "announcements.json");
    }

    public DemoDocument Load(out string notice)
    {
        notice = "Interações demonstrativas deste perfil local; não são votos de usuários autenticados.";
        if (!File.Exists(DataPath)) { fingerprint = null; return dataKind == "local-demo" ? Seed() : new DemoDocument { DataKind = dataKind }; }
        try
        {
            if (new FileInfo(DataPath).Length > 5_000_000) throw new InvalidDataException("Arquivo demonstrativo muito grande.");
            var bytes = File.ReadAllBytes(DataPath);
            fingerprint = Fingerprint(bytes);
            var data = JsonSerializer.Deserialize<DemoDocument>(bytes, Options)
                ?? throw new InvalidDataException("Documento vazio.");
            Validate(data);
            foreach (var item in data.Announcements) item.IsExample = dataKind == "local-demo";
            return data;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException)
        {
            if (dataKind != "local-demo")
                throw new InvalidDataException("Arquivo de anúncios cadastrados inválido; original preservado e gravação bloqueada.", ex);
            // Preserve the invalid document; never overwrite a future/unknown schema silently.
            var backup = DataPath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N");
            File.Copy(DataPath, backup);
            notice = $"Dados JSON inválidos. Demonstração reiniciada; original preservado em {backup}.";
            return Seed();
        }
    }

    public void Save(IEnumerable<Announcement> announcements)
    {
        var data = new DemoDocument { DataKind = dataKind, Announcements = announcements.ToList() };
        Validate(data);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(data, Options);
        if (payload.Length > 5_000_000) throw new InvalidDataException("JSON excede o limite de 5 MB; gravação recusada.");
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        using var writeLock = new FileStream(DataPath + ".write.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string? current = File.Exists(DataPath) ? Fingerprint(File.ReadAllBytes(DataPath)) : null;
        if (current != fingerprint) throw new IOException("Dados alterados por outra instância. Feche e reabra o mural antes de gravar.");
        var temporary = DataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, DataPath, overwrite: true);
            fingerprint = Fingerprint(payload);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void Validate(DemoDocument data)
    {
        if (data.Version != 1 || data.DataKind != dataKind || data.Announcements is null || data.Announcements.Count > 100)
            throw new InvalidDataException("Formato demonstrativo não reconhecido.");
        var ids = new HashSet<Guid>();
        foreach (var item in data.Announcements)
        {
            if (item is null || item.Id == Guid.Empty || !ids.Add(item.Id) || string.IsNullOrWhiteSpace(item.Title)
                || item.Title.Length > 200 || item.InitialLikes < 0 || item.InitialLikes > 1_000_000
                || !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.MediaKind) || item.Comments is null || item.Comments.Count > 500
                || item.Comments.Any(c => c is null || c.Id == Guid.Empty || c.AnnouncementId != item.Id
                    || string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 1000))
                throw new InvalidDataException("Publicação demonstrativa inválida.");
        }
    }

    public static DemoDocument CreateDemo() => Seed();

    private static DemoDocument Seed()
    {
        string[] titles = ["Encontro de integração", "Nossa biblioteca", "Bem-estar no trabalho", "Formação e conhecimento",
            "Agenda da semana", "Vídeo institucional", "Projetos em colaboração", "Segurança digital", "Espaço de ideias"];
        return new DemoDocument { Announcements = titles.Select((title, i) => new Announcement
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}"), Title = title,
            Description = i == 5 ? "Escolha um vídeo local para experimentar a reprodução. Nenhum vídeo é baixado." : "Comunicado fictício. Participe das conversas e acompanhe as novidades.",
            MediaKind = i == 5 ? MediaKind.Video : MediaKind.Image,
            MediaPath = i == 5 ? null : $"demo:{i}",
            ExternalUrl = i is 3 or 7 ? "https://learn.microsoft.com/pt-br/" : null,
            InitialLikes = 4 + i * 3, PublishedAt = DateTimeOffset.Now.AddDays(-i),
            Comments = [new DemoComment { AnnouncementId = Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}"), Text = "Comentário fictício para conhecer o mural." }]
        }).ToList() };
    }
}
