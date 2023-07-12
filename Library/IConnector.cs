namespace Net.Leksi.E6dWebApp;

public interface IConnector
{
    bool IsConnected { get; }
    TextReader Get(string path, object? parameter = null);
    HttpResponseMessage Send(HttpRequestMessage request, object? parameter = null);
    string GetLink(string path, object? parameter = null);
}
