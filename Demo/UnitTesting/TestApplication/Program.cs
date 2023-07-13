using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System.Diagnostics;

namespace Net.Leksi.E6dWebApp.Demo.UnitTesting.TestApplication;

public class Tests : Runner
{
    private Server _server = new();
    private IConnector _connector = null!;
    private readonly Dictionary<string, string> _pattern = new()
    {
        { "arozaupalanalapuazora", "arozaupalanalapuazora" },
        { "12345", "54321" },
    };
    private readonly MockStringProvider _stringProvider;

    public Tests()
    {
        _stringProvider = new()
        {
            Strings = _pattern.Keys.ToList(),
        };
    }

    [OneTimeSetUp]
    public void Setup()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
        Trace.AutoFlush = true;
        Start();
        _connector = GetConnector();
    }

    [OneTimeTearDown]
    public void Teardown()
    {
        Stop();
    }

    [Test]
    public void TestIfReverse()
    {
        for (int i = 0; i < _pattern.Count; ++i)
        {
            string response = _connector.Get($"/{i}").ReadToEnd();
            Assert.AreEqual(response, _pattern[_stringProvider.Strings[i]]);
        }
    }

    [Test]
    public void TestIfOutOfOrder()
    {
        int[] badIds = new int[] { -20, _pattern.Count, _pattern.Count + 50};
        for (int i = 0; i < badIds.Length; ++i)
        {
            AggregateException? aex = Assert.Throws<AggregateException>(() => _connector.Get($"/{badIds[i]}"));
            Assert.IsInstanceOf<HttpRequestException>(aex!.InnerExceptions[0], "Response status code does not indicate success: 404 (Not Found).");
        }
    }

    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews();
        mvcBuilder.AddApplicationPart(typeof(Server).Assembly);

        _server.ConfigureBuilder(builder);

        builder.Services.RemoveAll<IStringProvider>();
        builder.Services.AddSingleton<IStringProvider>(_stringProvider);
    }

    protected override void ConfigureApplication(WebApplication app)
    {
        _server.ConfigureApplication(app);
    }
}