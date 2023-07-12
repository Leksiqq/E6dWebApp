namespace Net.Leksi.E6dWebApp.Demo.Helloer;

public class Doer: Runner
{
    protected override void ConfigureApplication(WebApplication app)
    {
        app.Use(async (HttpContext context, Func<Task> next) => {
            string name = (context.RequestServices.GetRequiredService<RequestParameter>().Parameter as NameHolder)?.Name ?? "World";
            await context.Response.WriteAsync($"Hello {name}!");
        });
    }
}
