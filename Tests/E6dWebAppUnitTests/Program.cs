using Net.Leksi.E6dWebApp;
using NUnit.Framework;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;

namespace E6dWebAppUnitTests;

public class Program
{
    [OneTimeSetUp]
    public void Setup()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
        Trace.AutoFlush = true;
    }

    [Test]
    public void TestGetConnectorBeforeStart()
    {
        using Server server = new();
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => server.GetConnector());
        Assert.That(Equals(ex!.Message, "Runner is not running!"));
    }

    [Test]
    public void TestGetConnectorAffterStop()
    {
        using Server server = new();
        server.Start();
        server.Stop();
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => server.GetConnector());
        Assert.That(Equals(ex!.Message, "Runner is not running!"));
    }

    [Test]
    public void TestGetConnector()
    {
        using Server server = new();
        server.Start();
        Assert.DoesNotThrow(() => server.GetConnector());
        server.Stop();
    }

    [Test]
    public void TestHttpGetWhenDisconnected()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        server.Stop();
        InvalidOperationException? ex = Assert.Throws<InvalidOperationException>(() => connector.Get($"/TestHttpGet/{parameter}").ReadToEnd());
        Assert.That(Equals(ex!.Message, "Runner is not running!"));
    }

    [Test]
    public void TestHttpGet()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        Assert.That(connector.Get($"/TestHttpGet/{parameter}").ReadToEnd(), Is.EqualTo(parameter));
        server.Stop();
    }

    [Test]
    public void TestHttpGetWithParameter()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        StringBuilder sb = new();
        Assert.That(connector.Get($"/TestHttpGetWithParameter/{parameter}", sb).ReadToEnd(), Is.EqualTo(parameter));
        Assert.That(sb.ToString(), Is.EqualTo(parameter));
        server.Stop();
    }

    [Test]
    public void TestHttpGetWithCallback()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        Assert.That(
            connector.Get(
                $"/TestHttpGetWithCallback/",
                onRequest: cntx => cntx.RequestServices.GetRequiredService<StringBuilder>().Append(parameter)
            ).ReadToEnd(),
            Is.EqualTo(parameter)
        );
        server.Stop();
    }

    [Test]
    public void TestHttpGetWithBothParameterAndCallback()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        Assert.That(
            connector.Get(
                $"/TestHttpGetWithBothParameterAndCallback/",
                parameter: parameter,
                onRequest: cntx => cntx.RequestServices.GetRequiredService<StringBuilder>().Append(parameter)
            ).ReadToEnd(),
            Is.EqualTo("True")
        );
        server.Stop();
    }

    [Test]
    public void TestGetLink()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        string link = connector.GetLink($"/TestHttpGet/{parameter}");
        Assert.That(
            Regex.IsMatch(
                link,
                $"^http://localhost:\\d+/TestHttpGet/{parameter}\\?"
                + "[0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}=[0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}$"
            )
        );
        server.Stop();
    }

    [Test]
    public void TestGetLinkTemporaryRedirect()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        string link = connector.GetLink($"/TestHttpGet/{parameter}");
        Match m = Regex.Match(
                link,
                $"^(http://localhost:\\d+/TestHttpGet/{parameter})\\?"
                + "([0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}=[0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12})$"
            );
        Assert.That(m.Success);
        HttpClientHandler ch = new();
        ch.AllowAutoRedirect = false;
        HttpClient httpClient = new HttpClient(ch);
        HttpResponseMessage response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, link));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TemporaryRedirect));
        Assert.That(response.Headers.Contains("Location"));
        Assert.That(response.Headers.Location!.OriginalString, Is.EqualTo(m.Groups[1].Value));
        IEnumerable<string> cookies = response.Headers.SingleOrDefault(header => header.Key == "Set-Cookie").Value;
        foreach(var cookie in cookies)
        {
            Assert.That(Regex.IsMatch(cookie, "^" + m.Groups[2].Value + ";\\s+path=/"));
        }
        server.Stop();
    }

    [Test]
    public void TestGetLinkOk()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        string link = connector.GetLink($"/TestHttpGet/{parameter}");
        Match m = Regex.Match(
                link,
                $"^(http://localhost:\\d+/TestHttpGet/){parameter}\\?"
            );
        HttpClient httpClient = new HttpClient();
        HttpResponseMessage response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, link));
        Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(parameter));
        string parameter1 = "Once we get the \"cookies\" string, is there a clean way to obtain a specific cookie value";
        response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, m.Groups[1].Value + parameter1));
        Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(parameter1));
        server.Stop();
    }

    [Test]
    public void TestGetLinkAnotherBrowser()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        string link = connector.GetLink($"/TestHttpGet/{parameter}");
        Match m = Regex.Match(
                link,
                $"^(http://localhost:\\d+/TestHttpGet/){parameter}\\?"
            );
        HttpClient httpClient = new HttpClient();
        HttpResponseMessage response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, link));
        Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(parameter));
        string parameter1 = "Once we get the \"cookies\" string, is there a clean way to obtain a specific cookie value";
        HttpClient httpClient1 = new HttpClient();
        response = httpClient1.Send(new HttpRequestMessage(HttpMethod.Get, m.Groups[1].Value + parameter1));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        server.Stop();
    }

    [Test]
    public void TestGetLinkGetWithParameter()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        server.Start();
        IConnector connector = server.GetConnector();
        StringBuilder sb = new();
        string link = connector.GetLink($"/TestHttpGetWithParameter/{parameter}", parameter: sb);
        Match m = Regex.Match(
                link,
                $"^(http://localhost:\\d+/TestHttpGetWithParameter/){parameter}\\?"
            );
        HttpClient httpClient = new HttpClient();
        HttpResponseMessage response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, link));
        Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(parameter));
        Assert.That(sb.ToString(), Is.EqualTo(parameter));
        string parameter1 = "Once we get the \"cookies\" string, is there a clean way to obtain a specific cookie value";
        response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, m.Groups[1].Value + parameter1));
        Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(parameter1));
        Assert.That(sb.ToString(), Is.EqualTo(parameter + parameter1));
        server.Stop();
    }

    [Test]
    public void TestGetLinkFullCycle()
    {
        using Server server = new();
        string parameter = "sd;lfskldfklasdjlkasdflks'dkjal;sgfas";
        string parameter1 = "Once we get the \"cookies\" string, is there a clean way to obtain a specific cookie value";
        string link;
        HttpResponseMessage response;
        HttpClient httpClient = new HttpClient();
        Match m;
        server.Start();
        Func<string, string> reverse = str => new string(str.Reverse().ToArray());
        Action<HttpContext> onRequest = context =>
        {
            StringBuilder sb = (StringBuilder)context.RequestServices.GetRequiredService<RequestParameter>().Parameter!;
            string rev = reverse(sb.ToString());
            sb.Clear().Append(rev);
        };
        IConnector connector = server.GetConnector();
        {
            StringBuilder sb = new();
            link = connector.GetLink($"/TestGetLinkFullCycle", parameter: sb, onRequest: onRequest);
            m = Regex.Match(
                    link,
                    $"^(http://localhost:\\d+/TestGetLinkFullCycle)\\?"
                );
            sb.Append(parameter);
            response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, link));
            Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(reverse(parameter)));
            sb.Clear().Append(parameter1);
            response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, m.Groups[1].Value));
            Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(reverse(parameter1)));
            sb.Append(parameter);
        }
        response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, m.Groups[1].Value));
        Assert.That(new StreamReader(response.Content.ReadAsStream()).ReadToEnd(), Is.EqualTo(reverse(parameter) + parameter1));
        connector.ClearLink(link);
        response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, m.Groups[1].Value));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        server.Stop();
    }

}
