using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Diagnostics;
using System.Text;

namespace Net.Leksi.E6dWebApp;

public abstract class Runner : IDisposable
{
    private class RequestOptions
    {
        internal WeakReference? Parameter { get; set; }
        internal WeakReference<Action<HttpContext>>? OnRequest { get; set; }
        internal bool IsLinkRequest { get; set; } = false;
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

        public TextReader Get(string path, object? parameter = null, Action<HttpContext>? onRequest = null)
        {
            HttpResponseMessage response = Send(
                new HttpRequestMessage(HttpMethod.Get, path), parameter, onRequest
            );
            EnsureSuccessStatusCode(response);
            return new StreamReader(response.Content.ReadAsStream());
        }

        public HttpResponseMessage Send(HttpRequestMessage request, object? parameter = null, 
            Action<HttpContext>? onRequest = null)
        {
            PrepareRequest(
                request,
                parameter is { } || onRequest is { }
                ? new RequestOptions
                {
                    Parameter = parameter is { } ? new WeakReference(parameter) : null,
                    OnRequest = onRequest is { } ? new WeakReference<Action<HttpContext>>(onRequest) : null,
                } : null
            );
            return _client.Send(request);
        }

        public string GetLink(string path, object? parameter = null, Action<HttpContext>? onRequest = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, path);
            PrepareRequest(
                request,
                new RequestOptions
                {
                    Parameter = parameter is { } ? new WeakReference(parameter) : null,
                    OnRequest = onRequest is { } ? new WeakReference<Action<HttpContext>>(onRequest) : null,
                    IsLinkRequest = true,
                }
            );
            return new StreamReader(_client.Send(request).Content.ReadAsStream()).ReadToEnd();
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

        private void PrepareRequest(HttpRequestMessage request, RequestOptions? requestOptions)
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
            if (requestOptions is { })
            {
                _runner._parameters[_id].Add(serial, requestOptions);
            }
        }
    }

    private const string s_connectorIdHeaderName = "X-Connector-Id";
    private const string s_serialHeaderName = "X-Serial-Number";
    private const int s_maxTcpPort = 65535;
    private const int s_startTcpPort = 5000;

    private readonly object _syncObject = new();
    private readonly ManualResetEventSlim _appStartedGate = new();
    private readonly Dictionary<string, Connector> _connectors = new();
    private readonly Dictionary<string, Dictionary<string, RequestOptions>> _parameters = new();
    private readonly Dictionary<string, Dictionary<string, Tuple<string, RequestOptions?>>> _authorizedCookies = new();
    private WebApplication _app = null!;
    private bool _isDisposed = false;

    public bool IsRunning { get; private set; } = false;

    public Runner()
    {
        _appStartedGate.Reset();
    }

    ~Runner()
    {
        Dispose();
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
                mvcBuilder.AddApplicationPart(GetType().Assembly);

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

                            if (
                                _parameters[connectorId].TryGetValue(serial, out RequestOptions? requestOptions)
                            )
                            {
                                if (requestOptions.IsLinkRequest)
                                {
                                    await context.Response.WriteAsync(
                                        ProcessOneTimeUrlRequest(context.Request, requestOptions)
                                    );
                                    return;
                                }
                                if (
                                    requestOptions.OnRequest is { }
                                    && requestOptions.OnRequest.TryGetTarget(out Action<HttpContext>? callback)
                                )
                                {
                                    callback?.Invoke(context);
                                }
                                if (
                                    requestOptions.Parameter is { }
                                    && requestOptions.Parameter.IsAlive
                                )
                                {
                                    context.RequestServices.GetRequiredService<RequestParameter>().Parameter = 
                                        requestOptions.Parameter.Target;
                                }
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
                            _authorizedCookies[connectorId].TryGetValue(selectorKey!, out Tuple<string, RequestOptions?>? tuple)
                        )
                        {
                            if(tuple.Item2 is { })
                            {
                                if (
                                    tuple.Item2.OnRequest is { }
                                    && tuple.Item2.OnRequest.TryGetTarget(out Action<HttpContext>? callback)
                                )
                                {
                                    callback?.Invoke(context);
                                }
                                if (
                                    tuple.Item2.Parameter is { }
                                    && tuple.Item2.Parameter.IsAlive
                                )
                                {
                                    context.RequestServices.GetRequiredService<RequestParameter>().Parameter =
                                        tuple.Item2.Parameter.Target;
                                }
                            }
                            if (hasQuery)
                            {
                                string? oldCookie = context.Request.Cookies
                                    .Where(q => q.Value.Equals(connectorId) && _authorizedCookies.TryGetValue(q.Value, out var dict) && dict.ContainsKey(q.Key))
                                    .Select(q => q.Key)
                                    .FirstOrDefault();

                                if (
                                    oldCookie is { }
                                    && _authorizedCookies[connectorId].TryGetValue(oldCookie!, out Tuple<string, RequestOptions?>? tuple1)
                                    && tuple1.Item1.Equals($"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}")
                                )
                                {
                                    _authorizedCookies[connectorId].Remove(oldCookie);
                                }

                                context.Response.Cookies.Append(selectorKey!, connectorId);
                                context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                                context.Response.Headers.Add("Location", $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{queryString}");
                            }
                            else
                            {
                                await next.Invoke(context);
                            }
                            return;
                        }
                    }
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync(String.Empty);
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

    public void Stop()
    {
        if (IsRunning)
        {
            lock (_syncObject)
            {
                if (IsRunning)
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
            }
        }
    }

    public IConnector GetConnector()
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException("Runner is not running!");
        }
        _appStartedGate.Wait();
        if (!IsRunning)
        {
            throw new InvalidOperationException("Runner is not running!");
        }
        HttpClient client = new();
        client.BaseAddress = new Uri(_app.Urls.First());
        string connectorId = Guid.NewGuid().ToString();
        lock (_syncObject)
        {
            while (_connectors.ContainsKey(connectorId))
            {
                connectorId = Guid.NewGuid().ToString();
            }
            client.Timeout = Timeout.InfiniteTimeSpan;
            _parameters.Add(connectorId, new Dictionary<string, RequestOptions>());
            _authorizedCookies.Add(connectorId, new Dictionary<string, Tuple<string, RequestOptions?>>());
            Connector result = new Connector(client, this, connectorId);
            _connectors.Add(connectorId, result);
            return result;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            Stop();
        }
    }

    protected virtual void ConfigureBuilder(WebApplicationBuilder builder) { }

    protected virtual void ConfigureApplication(WebApplication app) { }

    protected virtual void OnException(Exception ex) { }

    private string ProcessOneTimeUrlRequest(HttpRequest request, RequestOptions? requestOptions)
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
                new Tuple<string, RequestOptions?>(sourceUrl, requestOptions)
            );
        }
        sb.Append(request.Query.Any() ? '&' : '?').Append(oneTimeCode).Append('=').Append(request.Headers[s_connectorIdHeaderName][0]);
        return sb.ToString();
    }

}
