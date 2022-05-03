using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Reflection;

namespace Net.Leksi.DocsRazorator;

public class DocsRazorator
{
    private const string SecretWordHeader = "X-Secret-Word";
    private const int MaxTcpPort = 65535;
    private const int StartTcpPort = 5000;
    private readonly HttpClient _client = new HttpClient();
    private readonly string secretWord = Guid.NewGuid().ToString();
    private WebApplication app;

    public async IAsyncEnumerable<KeyValuePair<string, string>> Generate(IEnumerable<object> requisite, 
        IEnumerable<string> requests)
    {
        ConcurrentQueue<KeyValuePair<string, string>> queue = new();
        ManualResetEventSlim manualReset = new();

        manualReset.Reset();

        Task loadTask = Task.Run(() =>
        {
            int port = MaxTcpPort + 1;
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
                    throw new Exception("No TCP port is available.");
                }

                WebApplicationBuilder builder = WebApplication.CreateBuilder(new string[] { });

                builder.Logging.ClearProviders();

                builder.Services.AddRazorPages();

                IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();
                foreach (Assembly assembly in assemblies)
                {
                    mvcBuilder.AddApplicationPart(assembly);
                }

                foreach (object obj in requisitors)
                {
                    builder.Services.AddSingleton(obj.GetType(), op =>
                    {
                        return obj;
                    });
                }

                app = builder.Build();

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
                    manualReset.Set();
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
            manualReset.Set();
        });

        manualReset.Wait();
        _client.BaseAddress = new Uri(app.Urls.First());
        _client.DefaultRequestHeaders.Add(SecretWordHeader, secretWord);
        foreach (string request in requests)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, request);

            HttpResponseMessage response = await _client.SendAsync(requestMessage);
            if (response.IsSuccessStatusCode)
            {
                yield return new KeyValuePair<string, string>(request, await response.Content.ReadAsStringAsync());
            }
        }
        await app.StopAsync();

    }
}
