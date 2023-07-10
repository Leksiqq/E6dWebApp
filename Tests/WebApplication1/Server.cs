using Net.Leksi.E6dWebApp;

namespace WebApplication1;

public class Server : Runner
{
    protected override void ConfigureApplication(WebApplication app)
    {
        app.Use(async (HttpContext context, Func<Task> next) => {
            await context.Response.WriteAsync("Hello world!");
        });
    }

    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
    }
}
