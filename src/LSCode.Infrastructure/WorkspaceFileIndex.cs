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
            ct.ThrowIfCancellationRequested();string[] files,folders;
            try{files=Directory.GetFiles(folder);folders=Directory.GetDirectories(folder);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){continue;}
            foreach(var file in files)
            {
                ct.ThrowIfCancellationRequested();
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                var extension=Path.GetExtension(file);
                if(!textOnly||WorkspaceSearch.TextExtensions.Contains(extension)||ExtraTextExtensions.Contains(extension))yield return file;
            }
            foreach(var child in folders)if(!WorkspaceSearch.IgnoredDirectories.Contains(Path.GetFileName(child))&&(File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);
        }
    }
}
