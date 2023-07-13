namespace Net.Leksi.E6dWebApp.Demo.UnitTesting;

public class Server
{
    public void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IStringProvider, StringProvider>();
        builder.Services.AddMvc();
    }

    public void ConfigureApplication(WebApplication app)
    {
        app.MapControllers();
    }
}
