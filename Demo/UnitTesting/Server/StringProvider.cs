using System.Text;

namespace Net.Leksi.E6dWebApp.Demo.UnitTesting;

public class StringProvider : IStringProvider
{
    private const int s_minStringLength = 5;
    private const int s_maxStringLength = 100;

    private readonly List<string?> _generated = new();
    private readonly Random _random = new();

    public string? Get(int id)
    {
        if(id < 0 || id > 100000)
        {
            return null;
        }
        lock(_generated)
        {
            if (_generated.Count <= id)
            {
                _generated.AddRange(Enumerable.Range(0, id - _generated.Count + 1).Select<int, string?>(i => null));
            }
            if (_generated[id] is null)
            {
                _generated[id] = MakeRandomString();
            }
            return _generated[id];
        }
    }

    private string MakeRandomString()
    {
        int length = s_minStringLength + _random.Next(s_maxStringLength - s_minStringLength + 1);
        StringBuilder sb = new();
        for(int i = 0; i < length; ++i)
        {
            int nextCharPos = _random.Next(62);
            char nextChar = (char)((nextCharPos < 26) ? ('a' + nextCharPos) : (nextCharPos < 52 ? ('A' + nextCharPos - 26) : ('0' + nextCharPos - 52)));
            sb.Append(nextChar);
        }
        return sb.ToString();
    }
}
