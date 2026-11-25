using LSCode.Application;
using Microsoft.Win32;
namespace LSCode.Desktop;
internal sealed class DesktopDialogs(System.Windows.Window owner) : IDesktopDialogs
{
    public string? OpenFile(string? filter = null) { var d = new OpenFileDialog { Filter = filter ?? "Tất cả file|*.*" }; return d.ShowDialog(owner) == true ? d.FileName : null; }
    public string[] OpenFiles() { var d = new OpenFileDialog { Filter = "Tất cả file|*.*", Multiselect = true }; return d.ShowDialog(owner) == true ? d.FileNames : []; }
    public string? OpenFolder() { var d = new OpenFolderDialog(); return d.ShowDialog(owner) == true ? d.FolderName : null; }
    public string? SaveFile(string? name, string? filter = null)
    {
        var extension = filter?.Contains("*.sql", StringComparison.OrdinalIgnoreCase) == true ? ".sql"
            : filter?.Contains("*.xml", StringComparison.OrdinalIgnoreCase) == true ? ".xml" : "";
        var d = new SaveFileDialog
        {
            FileName = System.IO.Path.GetFileName(name ?? "Untitled.sql"),
            Filter = filter ?? "Tất cả file|*.*",
            AddExtension = extension.Length > 0,
            DefaultExt = extension
        };
        return d.ShowDialog(owner) == true ? d.FileName : null;
    }
}
