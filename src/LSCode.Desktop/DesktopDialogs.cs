using LSCode.Application;
using Microsoft.Win32;
namespace LSCode.Desktop;
internal sealed class DesktopDialogs(System.Windows.Window owner) : IDesktopDialogs
{
    public string? OpenFile() { var d = new OpenFileDialog { Filter = "Tất cả file|*.*" }; return d.ShowDialog(owner) == true ? d.FileName : null; }
    public string[] OpenFiles() { var d = new OpenFileDialog { Filter = "Tất cả file|*.*", Multiselect = true }; return d.ShowDialog(owner) == true ? d.FileNames : []; }
    public string? OpenFolder() { var d = new OpenFolderDialog(); return d.ShowDialog(owner) == true ? d.FolderName : null; }
    public string? SaveFile(string? name) { var d = new SaveFileDialog { FileName = System.IO.Path.GetFileName(name ?? "Untitled.sql"), Filter = "Tất cả file|*.*" }; return d.ShowDialog(owner) == true ? d.FileName : null; }
}
