using Microsoft.AspNetCore.Diagnostics;
using System.Reflection;
using System.Text;
using static Net.Leksi.E6dWebApp.Runner;

namespace Net.Leksi.E6dWebApp;

public abstract class Runner : IDisposable
{
    private class OneTimeUrlRequest
    {
        internal object? Parameter { get; set; }
    }

    public class Connector : IConnector
    {
        private readonly HttpClient _client;
        private readonly Runner _runner;
        private readonly string _id;
        private int _serial = 0;

        public bool IsConnected
        {
            get
            {
                if (!_runner.IsRunning)
                {
                    return false;
                }
                _runner._appStartedGate.Wait();
                return _runner.IsRunning;
            }
        }

        internal Connector(HttpClient client, Runner generator, string id)
        {
            _client = client;
            _runner = generator;
            _id = id;
        }

        public TextReader Get(string path, object? parameter = null)
        {
            HttpResponseMessage response = Send(new HttpRequestMessage(HttpMethod.Get, path), parameter);
            EnsureSuccessStatusCode(response);
            return new StreamReader(response.Content.ReadAsStream());
        }

        public HttpResponseMessage Send(HttpRequestMessage request, object? parameter = null)
        {
            PrepareRequest(request, parameter);
            return _client.Send(request);
        }

        public string GetOneTimeUrl(string path, object? parameter = null)
        {
            return Get(path, new OneTimeUrlRequest { Parameter = parameter }).ReadToEnd();
        }

        private static void EnsureSuccessStatusCode(HttpResponseMessage response)
        {
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                string message = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                if (!string.IsNullOrEmpty(message))
                {
                    throw new AggregateException(
                        new Exception[] {
                        e,
                        new Exception(message)
                        }
                    );
                }
                throw;
            }
        }

        private void PrepareRequest(HttpRequestMessage request, object? parameter)
        {

            if (!_runner.IsRunning)
            {
                throw new InvalidOperationException("Runner is not running!");
            }
            _runner._appStartedGate.Wait();
            if (!_runner.IsRunning)
            {
                throw new InvalidOperationException("Runner is not running!");
            }
            string serial = Interlocked.Increment(ref _serial).ToString();
            request.Headers.Add(s_serialHeaderName, serial);
            request.Headers.Add(s_connectorIdHeaderName, _id);
            if (parameter is { })
            {
                _runner._parameters[_id].Add(serial, new WeakReference(parameter));
            }
        }
    }

    private const string s_connectorIdHeaderName = "X-Connector-Id";
    private const string s_serialHeaderName = "X-Serial-Number";
    private const int s_maxTcpPort = 65535;
    private const int s_startTcpPort = 5000;

    private readonly List<Assembly> _assemblies = new();
    private readonly Dictionary<Type, Tuple<object, ServiceLifetime>> _services = new();
    private readonly ManualResetEventSlim _appStartedGate = new();
    private readonly Dictionary<string, Connector> _connectors = new();
    private readonly Dictionary<string, Dictionary<string, WeakReference>> _parameters = new();
    private readonly Dictionary<string, Dictionary<string, Tuple<string, WeakReference?>>> _authorizedCookies = new();
    private WebApplication _app = null!;

    public bool IsRunning { get; private set; } = false;

    public Runner()
    {
        _appStartedGate.Reset();
    }

    protected abstract void ConfigureBuilder(WebApplicationBuilder builder);

    protected abstract void ConfigureApplication(WebApplication app);

    protected virtual void OnException(Exception ex) { }

    public void AddAssembly(Assembly assembly)
    {
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }
    }

    public void AddService(Type? serviceType, object implementation, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        if (implementation is null)
        {
            throw new ArgumentNullException(nameof(implementation));
        }
        Assembly assembly;
        if (serviceType is null)
        {
            serviceType = implementation.GetType();
        }
        if (implementation is Type type)
        {
            assembly = type.Assembly;
        }
        else if (implementation is Func<IServiceProvider, object> implementationMethod)
        {
            assembly = implementationMethod.GetMethodInfo().ReturnType.Assembly;
        }
        else
        {
            assembly = implementation.GetType().Assembly;
        }
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }
        if (!_services.ContainsKey(serviceType))
        {
            _services.Add(serviceType, new Tuple<object, ServiceLifetime>(implementation, lifetime));
        }
    }

    public void AddService<TService, TImplementation>(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        AddService(typeof(TService), typeof(TImplementation), lifetime);
    }

    public void AddService(Type serviceType, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        AddService(serviceType, serviceType, lifetime);
    }

    public void AddService<TService>(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        AddService(typeof(TService), typeof(TService), lifetime);
    }

    public void AddService<TService>(object obj, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        AddService(typeof(TService), obj, lifetime);
    }

    public void AddService(object obj, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {

        AddService(null, obj, lifetime);
    }

    public void Start()
    {
        IsRunning = true;
        Random rnd = new();

        Task loadTask = Task.Run(() =>
        {

            while (true)
            {
                int port = rnd.Next(s_maxTcpPort + 1 - s_startTcpPort) + s_startTcpPort;

                WebApplicationBuilder builder = WebApplication.CreateBuilder(new string[] { });

                builder.Logging.ClearProviders();

                ConfigureBuilder(builder);

                IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();
                foreach (Assembly assembly in _assemblies)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }

                foreach (KeyValuePair<Type, Tuple<object, ServiceLifetime>> entry in _services)
                {
                    if (entry.Value.Item1 is Type type)
                    {
                        builder.Services.Add(new ServiceDescriptor(entry.Key, type, entry.Value.Item2));
                    }
                    else if (entry.Value.Item1 is Func<IServiceProvider, object> method)
                    {
                        builder.Services.Add(new ServiceDescriptor(entry.Key, method, entry.Value.Item2));
                    }
                    else
                    {
                        builder.Services.Add(new ServiceDescriptor(entry.Key, entry.Value.Item1));
                    }
                }

                builder.Services.AddScoped<RequestParameter>();

                _app = builder.Build();

                _app.UseExceptionHandler(eapp =>
                {
                    eapp.Run(async context =>
                    {
                        var exceptionHandlerPathFeature =
                            context.Features.Get<IExceptionHandlerPathFeature>();

                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        if (exceptionHandlerPathFeature?.Error is Exception exception)
                        {
                            OnException(exception);
                            await context.Response.WriteAsync($"{exception.Message}\n{exception.StackTrace}");
                        }
                        else
                        {
                            await Task.CompletedTask;
                        }
                    });
                });

                _app.Use(async (context, next) =>
                {
                    string? selectorKey = null;
                    bool hasHeader = context.Request.Headers.ContainsKey(s_connectorIdHeaderName)
                        && context.Request.Headers[s_connectorIdHeaderName].Any()
                        && _connectors.ContainsKey(context.Request.Headers[s_connectorIdHeaderName][0]);
                    bool hasQuery = !hasHeader
                        && (selectorKey = context.Request.Query
                            .Where(q => _authorizedCookies.TryGetValue(q.Value, out var dict) && dict.ContainsKey(q.Key))
                            .Select(q => q.Key)
                            .FirstOrDefault()) is { };

                    bool hasCookie = !hasHeader && !hasQuery
                        && (selectorKey = context.Request.Cookies.Where(q => _authorizedCookies.TryGetValue(q.Value, out var dict) && dict.ContainsKey(q.Key))
                            .Select(q => q.Key)
                            .FirstOrDefault()) is { };
                    if (hasHeader || hasQuery || hasCookie)
                    {
                        string connectorId;
                        if (hasHeader)
                        {
                            string serial = context.Request.Headers[s_serialHeaderName][0];

                            connectorId = context.Request.Headers[s_connectorIdHeaderName][0];

                            if (_parameters[connectorId].TryGetValue(serial, out WeakReference? wr) && wr.IsAlive)
                            {
                                if (wr.Target is OneTimeUrlRequest oneTimeUrlRequest)
                                {
                                    await context.Response.WriteAsync(ProcessOneTimeUrlRequest(context.Request, oneTimeUrlRequest.Parameter));
                                    return;
                                }
                                context.RequestServices.GetRequiredService<RequestParameter>().Parameter = wr.Target;
                            }
                            await next.Invoke(context);

                            _parameters[connectorId].Remove(serial);

                            return;
                        }
                        connectorId = hasQuery ? context.Request.Query[selectorKey!] : context.Request.Cookies[selectorKey!]!;
                        string queryString;
                        if (hasCookie)
                        {
                            queryString = context.Request.QueryString.Value ?? string.Empty;
                        }
                        else if(context.Request.Query.Count == 1)
                        {
                            queryString = string.Empty;
                        }
                        else
                        {
                            queryString = context.Request.QueryString.Value!.Replace($"&{selectorKey}={connectorId}", string.Empty);
                        }
                        if (
                            _authorizedCookies[connectorId].TryGetValue(selectorKey!, out Tuple<string, WeakReference?>? tuple)
                            && tuple.Item1.Equals($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{queryString}")
                        )
                        {
                            if (tuple.Item2 is WeakReference wr1 && wr1.IsAlive)
                            {
                                context.RequestServices.GetRequiredService<RequestParameter>().Parameter = wr1.Target;
                            }
                            string? oldCookie = context.Request.Cookies
                                .Where(q => q.Value.Equals(connectorId) && _authorizedCookies.TryGetValue(q.Value, out var dict) && dict.ContainsKey(q.Key))
                                .Select(q => q.Key)
                                .FirstOrDefault();

                            if (
                                oldCookie is { }
                                && _authorizedCookies[connectorId].TryGetValue(oldCookie!, out Tuple<string, WeakReference?>? tuple1)
                                && tuple1.Item1.Equals($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}")
                            )
                            {
                                _authorizedCookies[connectorId].Remove(oldCookie);
                            }

                            context.Response.Cookies.Append(selectorKey!, connectorId);
                            await next.Invoke(context);
                            return;
                        }
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync(String.Empty);
                        return;
                    }
                });

                ConfigureApplication(_app);

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
                catch (IOException) { }
            }
        });

        _appStartedGate.Wait();

        if (loadTask.IsFaulted)
        {
            throw loadTask.Exception ?? new Exception("Unknown");
        }

    }

    private string ProcessOneTimeUrlRequest(HttpRequest request, object? parameter)
    {
        StringBuilder sb = new();
        sb.Append(request.Scheme).Append("://").Append(request.Host).Append(request.Path).Append(request.QueryString);
        string sourceUrl = sb.ToString();
        string oneTimeCode = string.Empty;
        lock (_authorizedCookies[request.Headers[s_connectorIdHeaderName][0]])
        {
            do
            {
                oneTimeCode = Guid.NewGuid().ToString();
            }
            while (request.Query.TryGetValue(oneTimeCode, out _) || _authorizedCookies[request.Headers[s_connectorIdHeaderName][0]].ContainsKey(oneTimeCode));
            _authorizedCookies[request.Headers[s_connectorIdHeaderName][0]].Add(
                oneTimeCode,
                new Tuple<string, WeakReference?>(sourceUrl, parameter is { } ? new WeakReference(parameter) : null)
            );
        }
        sb.Append(request.Query.Any() ? '&' : '?').Append(oneTimeCode).Append('=').Append(request.Headers[s_connectorIdHeaderName][0]);
        return sb.ToString();
    }

    public void Stop()
    {
        _appStartedGate.Reset();
        IsRunning = false;
        _appStartedGate.Set();
        _appStartedGate.Reset();
        Task.Run(async () =>
        {
            await _app.StopAsync();
        }).Wait();
    }

    public IConnector GetConnector()
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Runner is not started!");
        }
        _appStartedGate.Wait();
        if (!IsRunning)
        {
            throw new InvalidOperationException("Runner is not started!");
        }
        HttpClient client = new();
        client.BaseAddress = new Uri(_app.Urls.First());
        string connectorId = Guid.NewGuid().ToString();
        lock (_connectors)
        {
            while (_connectors.ContainsKey(connectorId))
            {
                connectorId = Guid.NewGuid().ToString();
            }
            client.Timeout = Timeout.InfiniteTimeSpan;
            _parameters.Add(connectorId, new Dictionary<string, WeakReference>());
            _authorizedCookies.Add(connectorId, new Dictionary<string, Tuple<string, WeakReference?>>());
            Connector result = new Connector(client, this, connectorId);
            _connectors.Add(connectorId, result);
            return result;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
