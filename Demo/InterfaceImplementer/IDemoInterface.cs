namespace Net.Leksi.E6dWebApp.Demo.InterfaceImplementer;

public interface IDemoInterface
{
    string Name { get; set; }
    int Count { get; set; }
    bool IsAwesome { get; set; }

    void Process(int level);
}
