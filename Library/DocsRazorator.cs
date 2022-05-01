using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;
using System.Collections.Concurrent;
using System.Reflection;

namespace Net.Leksi.DocsRazorator;

public class DocsRazorator
{
    internal const string SecretWordHeader = "X-Secret-Word";
    public IEnumerable<string> Generate(IEnumerable<object> requisite, IEnumerable<string> requests)
    {
        ConcurrentQueue<string> queue = new();
        ManualResetEventSlim manualReset = new();

        manualReset.Reset();

        Task loadTask = Task.Run(() =>
        {
            int port = 5000;
            List<Assembly> assemblies = new();
            List<object> requisitors = new();
            foreach (object obj in requisite)
            {
                if (obj is Assembly asm)
                {
                    if (!assemblies.Contains(asm))
                    {
                        assemblies.Add(asm);
                    }

                }
                else
                {
                    Assembly assembly = obj.GetType().Assembly;
                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                    if (!requisitors.Contains(obj))
                    {
                        requisitors.Add(obj);
                    }
                }
            }
            while (true)
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder(new string[] { });

                builder.Logging.ClearProviders();

                builder.Services.AddRazorPages();

                IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();
                foreach (Assembly assembly in assemblies)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }
                mvcBuilder.AddRazorRuntimeCompilation();

                builder.Services.Configure<MvcRazorRuntimeCompilationOptions>(options =>
                {
                    foreach (Assembly assembly in assemblies)
                    {
                        options.FileProviders.Add(new EmbeddedFileProvider(assembly));
                    }
                });

                foreach (object obj in requisitors)
                {
                    builder.Services.AddSingleton(obj.GetType(), op => obj);
                }

                WebApplication app = builder.Build();

                string secretWord = Guid.NewGuid().ToString();

                app.Use(async (context, next) =>
                {
                    if (!context.Request.Headers.ContainsKey(SecretWordHeader) || !context.Request.Headers[SecretWordHeader].Contains(secretWord))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("");
                    }
                    else
                    {
                        await next.Invoke(context);
                    }
                });

                app.MapRazorPages();

                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    Client client = new(new Uri(app.Urls.First()), secretWord);
                    foreach (string request in requests)
                    {
                        queue.Enqueue(client.Send(request));
                        manualReset.Set();
                    }
                    app.StopAsync();
                });

                app.Urls.Clear();
                app.Urls.Add($"http://localhost:{port}");
                try
                {
                    app.Run();
                    break;
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex);
                    ++port;
                }
            }
        });

        do
        {
            manualReset.Wait();
            manualReset.Reset();
            while (queue.TryDequeue(out string result))
            {
                yield return result;
            }
        }
        while (!loadTask.IsCompleted);

    }
}
