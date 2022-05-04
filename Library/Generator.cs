using Microsoft.AspNetCore.Diagnostics;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Reflection;

namespace Net.Leksi.DocsRazorator;

public class Generator
{
    private const string SecretWordHeader = "X-Secret-Word";
    private const int MaxTcpPort = 65535;
    private const int StartTcpPort = 5000;
    private readonly HttpClient _client = new HttpClient();
    private readonly string secretWord = Guid.NewGuid().ToString();

    public async IAsyncEnumerable<KeyValuePair<string, object>> Generate(IEnumerable<object> requisite, 
        IEnumerable<string> requests)
    {
        ConcurrentQueue<KeyValuePair<string, string>> queue = new();
        ManualResetEventSlim appStartedGate = new();
        WebApplication app = null!;

        Exception? razorPageException = null;

        appStartedGate.Reset();

        Task loadTask = Task.Run(() =>
        {
            int port = MaxTcpPort + 1;
            List<Assembly> assemblies = new();
            List<object> services = new();
            foreach (object obj in requisite)
            {
                if (obj is Assembly asm)
                {
                    if (!assemblies.Contains(asm))
                    {
                        assemblies.Add(asm);
                    }

                }
                else if (obj is KeyValuePair<Type, object> pair)
                {
                    Assembly assembly = pair.Value.GetType().Assembly;
                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                    if (services.Find(v => v is KeyValuePair<Type, object> p &&  p.Key == pair.Key && Object.ReferenceEquals(p.Value, pair.Value)) is null)
                    {
                        services.Add(obj);
                    }
                }
                else
                {
                    Assembly assembly = obj.GetType().Assembly;
                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                    if (!services.Contains(obj))
                    {
                        services.Add(obj);
                    }
                }
            }
            while (true)
            {
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                int[] usedPorts = ipGlobalProperties.GetActiveTcpConnections()
                        .Select(v => v.LocalEndPoint.Port).Where(v => v >= StartTcpPort).OrderBy(v => v).ToArray();
                for (int i = 1; i < usedPorts.Length; ++i)
                {
                    if (usedPorts[i] > usedPorts[i - 1] + 1)
                    {
                        port = usedPorts[i] - 1;
                        break;
                    }
                }
                if (port > MaxTcpPort)
                {
                    try
                    {
                        throw new Exception("No TCP port is available.");
                    }
                    finally
                    {
                        appStartedGate.Set();
                    }
                }

                WebApplicationBuilder builder = WebApplication.CreateBuilder(new string[] { });

                builder.Logging.ClearProviders();

                builder.Services.AddRazorPages();

                IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();
                foreach (Assembly assembly in assemblies)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }

                foreach (object obj in services)
                {
                    if(obj is KeyValuePair<Type, object> pair)
                    {
                        builder.Services.AddSingleton(pair.Key, op =>
                        {
                            return pair.Value;
                        });
                    }
                    else
                    {
                        builder.Services.AddSingleton(obj.GetType(), op =>
                        {
                            return obj;
                        });
                    }
                }

                app = builder.Build();

                app.UseExceptionHandler(eapp =>
                {
                    eapp.Run(async context =>
                    {
                        var exceptionHandlerPathFeature =
                            context.Features.Get<IExceptionHandlerPathFeature>();

                        razorPageException = exceptionHandlerPathFeature?.Error;
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    });
                });

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
                    appStartedGate.Set();
                });

                app.Urls.Clear();
                app.Urls.Add($"http://localhost:{port}");
                try
                {
                    app.Run();
                    break;
                }
                catch (IOException ex) { }
            }
        });

        appStartedGate.Wait();

        if (loadTask.IsFaulted)
        {
            throw loadTask.Exception;
        }
        _client.BaseAddress = new Uri(app.Urls.First());
        _client.DefaultRequestHeaders.Add(SecretWordHeader, secretWord);
        foreach (string request in requests)
        {
            razorPageException = null;

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, request);

            HttpResponseMessage response = await _client.SendAsync(requestMessage);

            if(razorPageException is { })
            {
                yield return new KeyValuePair<string, object>(request, razorPageException);
            }
            else if (response.IsSuccessStatusCode)
            {
                yield return new KeyValuePair<string, object>(request, await response.Content.ReadAsStringAsync());
            }
            
        }
        await app.StopAsync();

    }
}
