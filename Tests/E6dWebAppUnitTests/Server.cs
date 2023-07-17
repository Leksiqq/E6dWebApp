using Net.Leksi.E6dWebApp;
using System.Net;
using System.Text;

namespace E6dWebAppUnitTests;

public class Server: Runner
{
    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<StringBuilder>();
    }

    protected override void ConfigureApplication(WebApplication app)
    {
        app.Map("/TestHttpGet/{parameter}", (Func<HttpContext, string, Task>)(async (context, parameter) =>
        {
            await context.Response.WriteAsync(parameter);
        }));
        app.Map("/TestHttpGetWithParameter/{value}", (Func<HttpContext, string, Task>)(async (context, value) =>
        {
            if(context.RequestServices.GetRequiredService<RequestParameter>().Parameter is StringBuilder sb)
            {
                sb.Append(value);
            }
            await context.Response.WriteAsync(value);
        }));
        app.Map("/TestGetLinkFullCycle", (Func<HttpContext, Task>)(async (context) =>
        {
            if (context.RequestServices.GetRequiredService<RequestParameter>().Parameter is StringBuilder sb)
            {
                await context.Response.WriteAsync(sb.ToString());
                return;
            }
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            await context.Response.WriteAsync(string.Empty);
        }));
        app.Map("/TestHttpGetWithCallback/", (Func<HttpContext, Task>)(async (context) =>
        {
            StringBuilder sb = context.RequestServices.GetRequiredService<StringBuilder>();
            await context.Response.WriteAsync(sb.ToString());
        }));
        app.Map("/TestHttpGetWithBothParameterAndCallback/", (Func<HttpContext, Task>)(async (context) =>
        {
            StringBuilder sb = context.RequestServices.GetRequiredService<StringBuilder>();
            string parameter = (string)context.RequestServices.GetRequiredService<RequestParameter>().Parameter!;
            await context.Response.WriteAsync(sb.ToString().Equals(parameter).ToString());
        }));
        app.Map("/TestGetLink/{parameter}", (Func<HttpContext, string, Task>)(async (context, parameter) =>
        {
            await context.Response.WriteAsync(parameter);
        }));
    }
}
