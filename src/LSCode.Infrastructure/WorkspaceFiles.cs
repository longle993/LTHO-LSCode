namespace LSCode.Infrastructure;

internal static class WorkspaceFiles
{
    internal static async Task<string> DuplicateAsync(string path, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Đường dẫn file phải là đường dẫn đầy đủ.");
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous);
        var folder = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index <= 9999; index++)
        {
            ct.ThrowIfCancellationRequested();
            var suffix = index == 1 ? "_copy" : "_copy" + index;
            var destination = Path.Combine(folder, name + suffix + extension);
            FileStream output;
            try
            {
                output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous);
            }
            catch (IOException) when (File.Exists(destination) || Directory.Exists(destination))
            {
                continue;
            }
            try
            {
                await using (output)
                    await source.CopyToAsync(output, ct);
                return destination;
            }
            catch
            {
                // This operation owns the new file, including cleanup after a partial copy.
                File.Delete(destination);
                throw;
            }
        }
        throw new IOException("Không tìm được tên file bản sao còn trống.");
    }
}
