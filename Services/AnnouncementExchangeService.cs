using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using MuralInstitucional.Models;

namespace MuralInstitucional.Services;

// Public allowlist DTOs deliberately exclude all local interactions and filesystem paths.
public sealed record ExchangeManifest(int Version, Guid OriginId, DateTimeOffset ExportedAt);
public sealed record ExchangeAnnouncement(string Title, string? Description, AnnouncementType Type,
    string? ExternalUrl, DateTimeOffset PublishedAt, AnnouncementDistribution Distribution, MediaKind MediaKind, string? Media);

public sealed class AnnouncementImportPreview : IDisposable
{
    internal string DirectoryPath { get; }
    internal ExchangeAnnouncement Data { get; }
    public Guid OriginId { get; }
    public Announcement Announcement { get; }
    internal AnnouncementImportPreview(string directory, ExchangeManifest manifest, ExchangeAnnouncement data, string? media)
    {
        DirectoryPath = directory; Data = data; OriginId = manifest.OriginId;
        Announcement = new Announcement { Id = Guid.NewGuid(), IsExample = false, IsImported = true,
            ExchangeOriginId = OriginId, Title = data.Title, Description = data.Description, Type = data.Type,
            PublishedAt = data.PublishedAt, Distribution = data.Distribution, ExternalUrl = data.ExternalUrl,
            MediaKind = data.MediaKind, MediaPath = media };
    }
    public void Dispose()
    {
        // Only generated files in this private directory, never paths from the archive.
        foreach (var file in Directory.GetFiles(DirectoryPath)) File.Delete(file);
        Directory.Delete(DirectoryPath);
    }
}

public static class AnnouncementExchangeService
{
    public const long ArchiveLimit = ManagedMediaService.VideoLimit + 2 * 65536;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16,
        RespectRequiredConstructorParameters = true, AllowDuplicateProperties = false };
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(i =>
    {
        uint value = (uint)i;
        for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
        return value;
    }).ToArray();
    private static void Validate(ExchangeAnnouncement data)
    {
        if (string.IsNullOrWhiteSpace(data.Title) || data.Title.Length > 200 || data.Description?.Length > 2000
            || !Enum.IsDefined(data.Type) || !Enum.IsDefined(data.MediaKind) || !AnnouncementDistribution.IsValid(data.Distribution)
            || data.PublishedAt == default || data.ExternalUrl?.Length > 2048
            || data.ExternalUrl is not null && !ExternalLinkService.TryValidate(data.ExternalUrl, out _))
            throw new InvalidDataException("Dados públicos do anúncio inválidos.");
        if (data.Media is not null && !AllowedMedia(data.Media, data.MediaKind))
            throw new InvalidDataException("Nome ou extensão da mídia inválidos.");
    }
    private static bool AllowedMedia(string name, MediaKind kind) =>
        (kind == MediaKind.Image ? new[] { "media.png", "media.jpg", "media.jpeg" } : new[] { "media.avi", "media.mp4", "media.wmv" }).Contains(name);

    public static void Export(Announcement item, string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Escolha um caminho de destino com extensão .zip.");
        string? source = item.MediaPath;
        // Generated demo posters have no physical media. Missing associated files are errors.
        if (source?.StartsWith("demo:", StringComparison.Ordinal) == true) source = null;
        if (source is not null)
        {
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Mídia redirecionada recusada.");
            ManagedMediaService.Validate(source, item.MediaKind);
        }
        var data = new ExchangeAnnouncement(item.Title, item.Description, item.Type, item.ExternalUrl, item.PublishedAt,
            item.Distribution, item.MediaKind, source is null ? null : "media" + Path.GetExtension(source).ToLowerInvariant());
        Validate(data);
        string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                WriteJson(zip, "manifest.json", new ExchangeManifest(1, item.ExchangeOriginId ?? item.Id, DateTimeOffset.UtcNow));
                WriteJson(zip, "announcement.json", data);
                if (source is not null)
                {
                    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var target = zip.CreateEntry(data.Media!, CompressionLevel.NoCompression).Open();
                    CopyBounded(input, target, item.MediaKind == MediaKind.Image ? ManagedMediaService.ImageLimit : ManagedMediaService.VideoLimit);
                }
            }
            // Revalidate the completed snapshot before publishing it.
            using (Read(temporary)) { }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void WriteJson<T>(ZipArchive zip, string name, T value)
    {
        using var stream = zip.CreateEntry(name).Open();
        JsonSerializer.Serialize(stream, value, Options);
    }
    private static void CopyBounded(Stream input, Stream output, long limit)
    {
        byte[] buffer = new byte[81920]; long total = 0; int count;
        while ((count = input.Read(buffer)) != 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Conteúdo descomprimido excede o limite permitido.");
            output.Write(buffer, 0, count);
        }
    }
    private static T ReadJson<T>(ZipArchiveEntry entry)
    {
        using var bytes = new MemoryStream();
        ReadEntry(entry, bytes, 65536);
        return JsonSerializer.Deserialize<T>(bytes.ToArray(), Options) ?? throw new InvalidDataException("JSON vazio.");
    }
    private static void ReadEntry(ZipArchiveEntry entry, Stream output, long limit)
    {
        if (entry.Length > limit) throw new InvalidDataException("Entrada ZIP excede o limite permitido.");
        using var input = entry.Open();
        byte[] buffer = new byte[81920]; long total = 0; uint crc = uint.MaxValue; int count;
        while ((count = input.Read(buffer)) != 0)
        {
            total += count;
            if (total > limit || total > entry.Length) throw new InvalidDataException("Conteúdo ZIP excede o tamanho declarado.");
            for (int i = 0; i < count; i++) crc = CrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8);
            output.Write(buffer, 0, count);
        }
        if (total != entry.Length || ~crc != entry.Crc32) throw new InvalidDataException("Conteúdo ZIP corrompido (tamanho ou CRC inválido).");
    }
    public static AnnouncementImportPreview Read(string archive)
    {
        string directory = Path.Combine(Path.GetTempPath(), "MuralImport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var input = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > ArchiveLimit) throw new InvalidDataException("ZIP excede o limite de tamanho.");
            ValidateDirectory(input);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);
            if (zip.Entries.Count is < 2 or > 3) throw new InvalidDataException("Quantidade de entradas ZIP inválida.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName;
                if (!names.Add(name) || name is not ("manifest.json" or "announcement.json")
                    && !AllowedMedia(name, MediaKind.Image) && !AllowedMedia(name, MediaKind.Video)
                    || (entry.ExternalAttributes & 0x410) != 0 || ((entry.ExternalAttributes >> 16) & 0xF000) is not (0 or 0x8000)
                    || entry.Length < 0 || entry.Length > ManagedMediaService.VideoLimit
                    || entry.Length > Math.Max(65536L, entry.CompressedLength * 200L))
                    throw new InvalidDataException("Entrada ZIP inesperada, redirecionada ou excessiva.");
                total += entry.Length;
                if (total > ArchiveLimit) throw new InvalidDataException("ZIP descomprimido excede o limite.");
            }
            var manifest = ReadJson<ExchangeManifest>(zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Manifesto ausente."));
            if (manifest.Version != 1 || manifest.OriginId == Guid.Empty || manifest.ExportedAt == default)
                throw new InvalidDataException("Versão ZIP incompatível ou identificador de origem inválido.");
            var data = ReadJson<ExchangeAnnouncement>(zip.GetEntry("announcement.json") ?? throw new InvalidDataException("Anúncio ausente."));
            Validate(data);
            if (zip.Entries.Count != (data.Media is null ? 2 : 3)) throw new InvalidDataException("Mídia inesperada ou ausente.");
            string? path = null;
            if (data.Media is not null)
            {
                var entry = zip.GetEntry(data.Media) ?? throw new InvalidDataException("Mídia ausente.");
                path = Path.Combine(directory, "preview" + Path.GetExtension(data.Media));
                using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    ReadEntry(entry, target, data.MediaKind == MediaKind.Image ? ManagedMediaService.ImageLimit : ManagedMediaService.VideoLimit);
                ManagedMediaService.Validate(path, data.MediaKind);
            }
            return new AnnouncementImportPreview(directory, manifest, data, path);
        }
        catch
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
            throw;
        }
    }
    // Bound the central directory before ZipArchive materializes its entries in memory.
    // ZIP64 and multi-volume archives are unnecessary for this small, three-file format.
    private static void ValidateDirectory(FileStream input)
    {
        int length = (int)Math.Min(input.Length, 65557);
        var tail = new byte[length]; input.Position = input.Length - length; input.ReadExactly(tail);
        for (int i = length - 22; i >= 0; i--)
        {
            if (BitConverter.ToUInt32(tail, i) != 0x06054b50 || i + 22 + BitConverter.ToUInt16(tail, i + 20) != length) continue;
            int entries = BitConverter.ToUInt16(tail, i + 10);
            uint size = BitConverter.ToUInt32(tail, i + 12), offset = BitConverter.ToUInt32(tail, i + 16);
            if (BitConverter.ToUInt16(tail, i + 4) != 0 || BitConverter.ToUInt16(tail, i + 6) != 0
                || BitConverter.ToUInt16(tail, i + 8) != entries || entries is < 2 or > 3 || size > 4096
                || (long)offset + size != input.Length - length + i)
                throw new InvalidDataException("Diretório ZIP inválido, excessivo, ZIP64 ou multivolume.");
            input.Position = 0; return;
        }
        throw new InvalidDataException("ZIP corrompido ou incompleto.");
    }
    public static bool IsDuplicate(IEnumerable<Announcement> existing, AnnouncementImportPreview preview) =>
        existing.Any(x => x.ExchangeOriginId == preview.OriginId || x.Id == preview.OriginId);

    public static Announcement Import(IAnnouncementRepository repository, ManagedMediaService media,
        IEnumerable<Announcement> existing, AnnouncementImportPreview preview, bool independentCopy = false)
    {
        var items = existing.Where(x => !x.IsExample).ToList();
        if (IsDuplicate(existing, preview) && !independentCopy) throw new InvalidOperationException("Este anúncio já foi importado. Cancele ou confirme uma cópia independente.");
        Validate(preview.Data);
        string? path = null;
        try
        {
            if (preview.Announcement.MediaPath is { } source) path = media.Import(source, preview.Data.MediaKind);
            var data = preview.Data;
            var result = new Announcement { Id = Guid.NewGuid(), IsExample = false, IsImported = true,
                ExchangeOriginId = preview.OriginId, Title = data.Title, Description = data.Description, Type = data.Type,
                ExternalUrl = data.ExternalUrl, PublishedAt = data.PublishedAt,
                Distribution = data.Distribution with { Criteria = data.Distribution.Criteria.ToList() }, MediaKind = data.MediaKind, MediaPath = path };
            items.Insert(0, result);
            repository.Save(items);
            return result;
        }
        catch { if (path is not null) media.DeleteOwned(path); throw; }
    }
}
