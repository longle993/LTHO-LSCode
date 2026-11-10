using System.Windows;
namespace LSCode.Desktop;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke=e.Args.Contains("--smoke");
        if(smoke)
        {
            var data=Environment.GetEnvironmentVariable("LSCODE_DATA_HOME");
            if(string.IsNullOrEmpty(data)||!System.IO.File.Exists(System.IO.Path.Combine(data,"webview-smoke.marker")))
            {
                data=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"LSCode-WebView-smoke-"+Guid.NewGuid().ToString("N"));
                var workspace=System.IO.Path.Combine(data,"workspace");System.IO.Directory.CreateDirectory(workspace);
                System.IO.File.WriteAllText(System.IO.Path.Combine(workspace,"fixture.xml"),"<root>Fixture</root>",new System.Text.UnicodeEncoding(false,true,true));
                System.IO.File.WriteAllText(System.IO.Path.Combine(data,"settings.json"),System.Text.Json.JsonSerializer.Serialize(new{Theme="Dark",Workspace=workspace}));
                Environment.SetEnvironmentVariable("LSCODE_DATA_HOME",data);
            }
        }
        new StudioWindow(smoke).Show();
    }
}
