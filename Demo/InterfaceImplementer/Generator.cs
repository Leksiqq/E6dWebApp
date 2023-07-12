using Net.Leksi.E6dWebApp.Demo.InterfaceImplementer.Pages;
using System.Diagnostics;

namespace Net.Leksi.E6dWebApp.Demo.InterfaceImplementer;

public class Generator: Runner
{
    public void Implement(Type @interface)
    {
        Start();

        IConnector connector = GetConnector();

        TextReader reader = connector.Get("/Implementation", @interface);

        Process.Start(new ProcessStartInfo
        {
            FileName = "notepad.exe",
            UseShellExecute = true
        });

    }

    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton(this);
    }

    protected override void ConfigureApplication(WebApplication app)
    {
        app.MapRazorPages();
    }

    internal void Generate(ImplementationModel model)
    {
    }
}
