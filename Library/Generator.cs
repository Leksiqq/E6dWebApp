using Microsoft.AspNetCore.Diagnostics;
using System.Reflection;

namespace Net.Leksi.TextGenerator;

public class Generator
{
    public class Connector
    {
        private readonly HttpClient _client;

        internal Connector(HttpClient client)
        {
            _client = client;
        }

        public HttpResponse Get(string path, object? parameters)
        {
            _client
        }
    }

    private const string SecretWordHeader = "X-Connector-Id";
    private const int MaxTcpPort = 65535;
    private const int StartTcpPort = 5000;

    private readonly List<Assembly> _assemblies = new();
    private readonly List<object> _services = new();
    private readonly ManualResetEventSlim _appStartedGate = new();
    private readonly Dictionary<string, Connector> _connectors = new();

    private bool _running = false;
    private string _secretWord = Guid.NewGuid().ToString();
    private WebApplication _app = null!;

    public void AddRequisite(object requisiteItem)
    {
        if (requisiteItem is Assembly asm)
        {
            if (!_assemblies.Contains(asm))
            {
                _assemblies.Add(asm);
            }

        }
        else if (requisiteItem is KeyValuePair<Type, Type> typeTypePair)
        {
            Assembly assembly = typeTypePair.Value.Assembly;
            if (!_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
            }
            if (_services.Find(v => v is KeyValuePair<Type, object> p && p.Key == typeTypePair.Key
                && Object.ReferenceEquals(p.Value, typeTypePair.Value)) is null)
            {
                _services.Add(requisiteItem);
            }
        }
        else if (requisiteItem is KeyValuePair<Type, object> typeObjectPair)
        {
            Assembly assembly = typeObjectPair.Value.GetType().Assembly;
            if (!_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
            }
            if (_services.Find(v => v is KeyValuePair<Type, object> p && p.Key == typeObjectPair.Key
                && Object.ReferenceEquals(p.Value, typeObjectPair.Value)) is null)
            {
                _services.Add(requisiteItem);
            }
        }
        else if (requisiteItem is Type type)
        {
            Assembly assembly = type.Assembly;
            if (!_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
            }
            if (!_services.Contains(requisiteItem))
            {
                _services.Add(requisiteItem);
            }
        }
        else
        {
            Assembly assembly = requisiteItem.GetType().Assembly;
            if (!_assemblies.Contains(assembly))
            {
                _assemblies.Add(assembly);
            }
            if (!_services.Contains(requisiteItem))
            {
                _services.Add(requisiteItem);
            }
        }
    }

    public void Start()
    {
        _appStartedGate.Reset();

        Task loadTask = Task.Run(() =>
        {
            Exception? razorPageException = null;

            int port = StartTcpPort - 1;
            while (true)
            {
                _secretWord = Guid.NewGuid().ToString();
                ++port;
                if (port > MaxTcpPort)
                {
                    try
                    {
                        throw new Exception("No TCP port is available.");
                    }
                    finally
                    {
                        _appStartedGate.Set();
                    }
                }

                WebApplicationBuilder builder = WebApplication.CreateBuilder(new string[] { });

                builder.Logging.ClearProviders();

                builder.Services.AddRazorPages();

                IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();
                foreach (Assembly assembly in _assemblies)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }

                foreach (object obj in _services)
                {
                    if (obj is KeyValuePair<Type, Type> typeTypePair)
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
                    else if (obj is Type type)
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

                _app = builder.Build();

                _app.UseExceptionHandler(eapp =>
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

                _app.Use(async (context, next) =>
                {
                    if (
                        !context.Request.Headers.ContainsKey(SecretWordHeader) 
                        || context.Request.Headers[SecretWordHeader].Count == 0
                        || !_connectors.ContainsKey(context.Request.Headers[SecretWordHeader][0])
                    )
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync(String.Empty);
                    }
                    else
                    {
                        await next.Invoke(context);
                        if (context.Response.StatusCode != StatusCodes.Status200OK)
                        {
                            razorPageException = new Exception($"Page {context.Request.Path}{context.Request.QueryString} processing error. Code: {context.Response.StatusCode}");
                        }
                    }
                });

                _app.MapRazorPages();

                _app.Lifetime.ApplicationStarted.Register(() =>
                {
                    _appStartedGate.Set();
                });

                _app.Urls.Clear();
                _app.Urls.Add($"http://localhost:{port}");
                try
                {
                    _app.Run();
                    break;
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex);
                }
            }
        });

        _appStartedGate.Wait();

        if (loadTask.IsFaulted)
        {
            throw loadTask.Exception ?? new Exception("Unknown");
        }

    }

    public Connector GetConnector()
    {
        _appStartedGate.Wait();
        HttpClient client = new();
        client.BaseAddress = new Uri(_app.Urls.First());
        client.DefaultRequestHeaders.Add(SecretWordHeader, _secretWord);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return new Connector(client);
    }

    public static async IAsyncEnumerable<KeyValuePair<string, object>> Generate(IEnumerable<object> requisite, 
        IEnumerable<string> requests)
    {
        ManualResetEventSlim appStartedGate = new();
        WebApplication app = null!;
        HttpClient client = null!;

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
                    if (!context.Request.Headers.ContainsKey(SecretWordHeader) || !context.Request.Headers[SecretWordHeader].Contains(_secretWord))
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
        client.DefaultRequestHeaders.Add(SecretWordHeader, _secretWord);
        client.Timeout = Timeout.InfiniteTimeSpan;
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
