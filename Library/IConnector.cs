namespace Net.Leksi.E6dWebApp;

public interface IConnector
{
    bool IsConnected { get; }
    bool RedirectConsoleOut { get; set; }
    TextReader Get(string path, object? parameter = null, Action<HttpContext>? onRequest = null);
    HttpResponseMessage Send(HttpRequestMessage request, object? parameter = null, Action<HttpContext>? onRequest = null);
    string GetLink(string path, object? parameter = null, Action<HttpContext>? onRequest = null);
    void ClearLink(string link);
}
