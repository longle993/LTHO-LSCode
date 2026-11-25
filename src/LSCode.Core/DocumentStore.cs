using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LSCode.Core;

public sealed record FileSnapshot(string Path, string Text, Encoding Encoding, bool Bom, string Hash)
{
    public string Newline => Text.Contains("\r\n") ? "CRLF" : "LF";
}

public sealed class FileConflictException(string path) : IOException($"File đã thay đổi bên ngoài IDE: {path}");

public static class DocumentStore
{
    private static readonly Regex XmlDeclarationEncoding = new(
        """\A(<\?xml\s+version\s*=\s*['"][^'"]+['"]\s+encoding\s*=\s*)(['"])[^'"]+\2(?=[^?]*\?>)""",
        RegexOptions.CultureInvariant);
    public const int MaximumFileBytes = 16 * 1024 * 1024;
    public static async Task<FileSnapshot> OpenAsync(string path, CancellationToken ct = default)
    {
        path = System.IO.Path.GetFullPath(path);
        var bytes = await ReadBytesAsync(path, ct);
        var (encoding, skip) = DetectEncoding(bytes);
        var text = DecodeText(bytes, encoding, skip);
        return new(path, text, encoding, skip > 0, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    internal static async Task<string> ReadTextAsync(string path, CancellationToken ct)
    {
        var bytes = await ReadBytesAsync(path, ct);
        var (encoding, skip) = DetectEncoding(bytes);
        return DecodeText(bytes, encoding, skip);
    }

    private static Task<byte[]> ReadBytesAsync(string path, CancellationToken ct)
    {
        if (new FileInfo(path).Length > MaximumFileBytes) throw new IOException("File vượt giới hạn editor 16 MB.");
        return File.ReadAllBytesAsync(path, ct);
    }

    private static string DecodeText(byte[] bytes, Encoding encoding, int skip)
    {
        var text = encoding.GetString(bytes, skip, bytes.Length - skip);
        if (text.Contains('\0')) throw new IOException("File binary hoặc encoding không được hỗ trợ.");
        return text;
    }

    public static async Task<FileSnapshot> SaveAsync(string path, string text, FileSnapshot? expected, CancellationToken ct = default, bool overwriteReadOnly = false)
    {
        path = System.IO.Path.GetFullPath(path);
        var restoreReadOnly = overwriteReadOnly && File.Exists(path) &&
            (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
        if (restoreReadOnly) File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        try { return await SaveWritableAsync(path, text, expected, ct); }
        finally
        {
            if (restoreReadOnly && File.Exists(path))
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
    }

    private static async Task<FileSnapshot> SaveWritableAsync(string path, string text, FileSnapshot? expected, CancellationToken ct)
    {
        path = System.IO.Path.GetFullPath(path);
        if (expected != null && string.Equals(expected.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) || Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path, ct))) != expected.Hash)
                throw new FileConflictException(path);
        }
        var bom = expected?.Bom == true;
        var xml = System.IO.Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase);
        var encoding = xml && bom ? new UnicodeEncoding(false, true, true)
            : xml ? new UTF8Encoding(false, true) : expected?.Encoding ?? new UTF8Encoding(false, true);
        if (xml)
            text = XmlDeclarationEncoding.Replace(text, "$1$2" + encoding.WebName + "$2", 1);
        var data = encoding.GetBytes(text);
        var preamble = bom ? encoding.GetPreamble() : [];
        var bytes = preamble.Concat(data).ToArray();
        var sameFile = expected != null && string.Equals(expected.Path, path, StringComparison.OrdinalIgnoreCase);
        // Open without truncating and deny concurrent writers during conflict checking and saving.
        await using var target = new FileStream(path, sameFile ? FileMode.Open : FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (sameFile)
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(target, ct));
            if (hash != expected!.Hash) throw new FileConflictException(path);
        }
        ct.ThrowIfCancellationRequested();
        target.Position = 0;
        // Finish a started write even if cancellation is requested.
        await target.WriteAsync(bytes, CancellationToken.None);
        target.SetLength(bytes.Length);
        await target.FlushAsync(CancellationToken.None);
        return new(path, text, encoding, bom, Convert.ToHexString(SHA256.HashData(bytes)));
    }
    private static (Encoding, int) DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) return (new UTF32Encoding(false, true, true), 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) return (new UTF32Encoding(true, true, true), 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return (new UTF8Encoding(true, true), 3);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) return (new UnicodeEncoding(false, true, true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) return (new UnicodeEncoding(true, true, true), 2);
        return (new UTF8Encoding(false, true), 0);
    }
}
