using Microsoft.AspNetCore.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;

namespace Net.Leksi.DocsRazorator;

public static class Generator
{
    private const string SecretWordHeader = "X-Secret-Word";
    private const int MaxTcpPort = 65535;
    private const int StartTcpPort = 5000;

    public static async IAsyncEnumerable<KeyValuePair<string, object>> Generate(IEnumerable<object> requisite, 
        IEnumerable<string> requests)
    {
        ManualResetEventSlim appStartedGate = new();
        WebApplication app = null!;
        HttpClient client = null!;
        string secretWord = Guid.NewGuid().ToString();

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
                else if (obj is KeyValuePair<Type, Type> typeTypePair)
                {
                    Assembly assembly = typeTypePair.Value.Assembly;
                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                    if (services.Find(v => v is KeyValuePair<Type, object> p && p.Key == typeTypePair.Key 
                        && Object.ReferenceEquals(p.Value, typeTypePair.Value)) is null)
                    {
                        services.Add(obj);
                    }
                }
                else if (obj is KeyValuePair<Type, object> typeObjectPair)
                {
                    Assembly assembly = typeObjectPair.Value.GetType().Assembly;
                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                    if (services.Find(v => v is KeyValuePair<Type, object> p && p.Key == typeObjectPair.Key 
                        && Object.ReferenceEquals(p.Value, typeObjectPair.Value)) is null)
                    {
                        services.Add(obj);
                    }
                }
                else if (obj is Type type)
                {
                    Assembly assembly = type.Assembly;
                    if (!assemblies.Contains(assembly))
                    {
                        assemblies.Add(assembly);
                    }
                    if (!services.Contains(obj))
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
            port = 1024;
            while (true)
            {
                ++port;
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
                    if(obj is KeyValuePair<Type, Type> typeTypePair)
                    {
                            builder.Services.AddTransient(typeTypePair.Key, typeTypePair.Value);
                    }
                    else if (obj is KeyValuePair<Type, object> typeObjectPair)
                    {
                        builder.Services.AddSingleton(typeObjectPair.Key, op =>
                        {
                            return typeObjectPair.Value;
                        });
                    }
                    else if(obj is Type type)
                    {
                        builder.Services.AddTransient(type);
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
                        await Task.CompletedTask;
                    });
                });

                app.Use(async (context, next) =>
                {
                    if (!context.Request.Headers.ContainsKey(SecretWordHeader) || !context.Request.Headers[SecretWordHeader].Contains(secretWord))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync(String.Empty);
                    }
                    else
                    {
                        await next.Invoke(context);
                        if(context.Response.StatusCode != StatusCodes.Status200OK)
                        {
                            razorPageException = new Exception($"Page {context.Request.Path}{context.Request.QueryString} processing error. Code: {context.Response.StatusCode}");
                        }
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
                catch (IOException ex) {
                    Console.WriteLine(ex);
                }
            }
        });

        appStartedGate.Wait();

        if (loadTask.IsFaulted)
        {
            throw loadTask.Exception;
        }
        client = new HttpClient();
        client.BaseAddress = new Uri(app.Urls.First());
        client.DefaultRequestHeaders.Add(SecretWordHeader, secretWord);
        foreach (string request in requests)
        {
            razorPageException = null;

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, request);

            HttpResponseMessage response = await client.SendAsync(requestMessage);

            if(razorPageException is { })
            {
                yield return new KeyValuePair<string, object>(request, razorPageException);
            }
            else
            {
                yield return new KeyValuePair<string, object>(request, await response.Content.ReadAsStringAsync());
            }
            
        }
        await app.StopAsync();

    }
}
