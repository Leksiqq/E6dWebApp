namespace Net.Leksi.DocsRazorator;

internal class Client
{
    private readonly HttpClient _client = new HttpClient();

    internal Client(Uri baseAddress, string secretWord)
    {
        _client.BaseAddress = baseAddress;
        _client.DefaultRequestHeaders.Add(DocsRazorator.SecretWordHeader, secretWord);
    }
    internal string Send(string request)
    {
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, request);

        HttpResponseMessage response = _client.Send(requestMessage);

        if (response.IsSuccessStatusCode)
        {
            return response.Content.ReadAsStringAsync().Result;
        }
        throw new NotImplementedException(request);
    }
}
