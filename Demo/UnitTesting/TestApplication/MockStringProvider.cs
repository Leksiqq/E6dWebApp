namespace Net.Leksi.E6dWebApp.Demo.UnitTesting.TestApplication;

public class MockStringProvider : IStringProvider
{
    internal List<string> Strings { get; init; } = null!;

    public string? Get(int id)
    {
        if(id >= 0 && id < Strings.Count)
        {
            return Strings[id];
        }
        return null;
    }
}
