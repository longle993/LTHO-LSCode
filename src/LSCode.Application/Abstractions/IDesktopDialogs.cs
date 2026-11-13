namespace LSCode.Application;

public interface IDesktopDialogs
{
    string? OpenFile();
    string[] OpenFiles();
    string? OpenFolder();
    string? SaveFile(string? name);
}
