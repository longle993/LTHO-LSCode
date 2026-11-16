namespace LSCode.Application;

public interface IDesktopDialogs
{
    string? OpenFile(string? filter = null);
    string[] OpenFiles();
    string? OpenFolder();
    string? SaveFile(string? name, string? filter = null);
}
