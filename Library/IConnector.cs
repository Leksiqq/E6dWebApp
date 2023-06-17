namespace Net.Leksi.TextGenerator;

public interface IConnector
{
    bool IsConnected { get; }
    TextReader Get(string path, object? parameter = null);
    Task<TextReader> GetAsync(string path, object? parameter = null);
    HttpResponseMessage Send(HttpRequestMessage request, object? parameter = null);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, object? parameter = null);
}
