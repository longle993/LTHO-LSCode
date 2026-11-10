using LSCode.Core;
namespace LSCode.Infrastructure;

internal static class WorkspaceFileIndex
{
    public static readonly HashSet<string> ExtraTextExtensions=new(StringComparer.OrdinalIgnoreCase){".f",".ent",".aspx",".htm",".cshtml",".razor"};
    public static IEnumerable<string> Files(string root,CancellationToken ct,bool textOnly=true)
    {
        var pending=new Stack<string>();pending.Push(root);
        while(pending.TryPop(out var folder))
        {
            ct.ThrowIfCancellationRequested();
            FileSystemInfo[] entries;
            try { entries = new DirectoryInfo(folder).GetFileSystemInfos(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (entry is DirectoryInfo)
                {
                    if (!WorkspaceSearch.IgnoredDirectories.Contains(entry.Name)) pending.Push(entry.FullName);
                    continue;
                }
                if (!textOnly || WorkspaceSearch.TextExtensions.Contains(entry.Extension) || ExtraTextExtensions.Contains(entry.Extension))
                    yield return entry.FullName;
            }
        }
    }
}
