using System.IO;
using System.Windows.Media.Imaging;
using MuralInstitucional.Models;

namespace MuralInstitucional.Services;

public sealed class ManagedMediaService(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public const long ImageLimit = 20 * 1024 * 1024;
    public const long VideoLimit = 250 * 1024 * 1024;
    public static void Validate(string path, MediaKind kind)
    {
        if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path).StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("Selecione um arquivo de disco local com caminho absoluto.");
        string extension = Path.GetExtension(path).ToLowerInvariant();
        string[] allowed = kind == MediaKind.Image ? [".png", ".jpg", ".jpeg"] : [".avi", ".mp4", ".wmv"];
        if (!Enum.IsDefined(kind) || !allowed.Contains(extension)) throw new InvalidDataException("Extensão incompatível com o tipo de mídia.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length == 0 || stream.Length > (kind == MediaKind.Image ? ImageLimit : VideoLimit))
            throw new InvalidDataException("Arquivo vazio ou excessivo: imagens até 20 MB; vídeos até 250 MB.");
        var header = new byte[16]; int count = stream.Read(header);
        bool signature = extension switch
        {
            ".png" => count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}),
            ".jpg" or ".jpeg" => count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
            ".avi" => count >= 12 && System.Text.Encoding.ASCII.GetString(header,0,4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header,8,4) == "AVI ",
            ".mp4" => count >= 12 && System.Text.Encoding.ASCII.GetString(header,4,4) == "ftyp",
            ".wmv" => count == 16 && header.AsSpan().SequenceEqual(new byte[] {48,38,178,117,142,102,207,17,166,217,0,170,0,98,206,108}),
            _ => false
        };
        if (!signature) throw new InvalidDataException("Conteúdo não corresponde à extensão da mídia.");
        if (kind == MediaKind.Image)
        {
            stream.Position = 0;
            try
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                if ((long)frame.PixelWidth * frame.PixelHeight > 40_000_000) throw new InvalidDataException("Imagem excede 40 megapixels.");
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException or System.Runtime.InteropServices.COMException)
            { throw new InvalidDataException("Imagem não pode ser decodificada pelo Windows.", ex); }
        }
    }
    public string Import(string source, MediaKind kind)
    {
        Validate(source, kind);
        Directory.CreateDirectory(DirectoryPath);
        EnsureDirectory();
        string target = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N") + Path.GetExtension(source).ToLowerInvariant());
        string temporary = target + ".tmp";
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920]; long total = 0; int read;
                while ((read = input.Read(buffer)) > 0)
                {
                    total += read;
                    if (total > (kind == MediaKind.Image ? ImageLimit : VideoLimit)) throw new InvalidDataException("Mídia excedeu o limite durante a cópia.");
                    output.Write(buffer, 0, read);
                }
                output.Flush(true);
            }
            File.Move(temporary, target); // No overwrite; unique file per import.
            Validate(target, kind);
            return target;
        }
        catch { DeleteOwned(target); throw; }
        finally { if (File.Exists(temporary)) { EnsureDirectory(); File.Delete(temporary); } }
    }
    public bool Owns(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return false;
        var full = Path.GetFullPath(path);
        return string.Equals(Path.GetDirectoryName(full), DirectoryPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(full), "N", out _)
            && new[] { ".png", ".jpg", ".jpeg", ".avi", ".mp4", ".wmv" }.Contains(Path.GetExtension(full).ToLowerInvariant());
    }
    private void EnsureDirectory()
    {
        if ((File.GetAttributes(DirectoryPath) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Pasta de mídia redirecionada; operação recusada.");
    }
    public void DeleteOwned(string? path)
    {
        if (!Owns(path) || !File.Exists(path)) return;
        EnsureDirectory();
        if ((File.GetAttributes(path!) & FileAttributes.ReparsePoint) != 0) throw new IOException("Arquivo de mídia redirecionado; remoção recusada.");
        File.Delete(Path.GetFullPath(path!));
    }
}
