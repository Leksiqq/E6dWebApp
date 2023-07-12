using Net.Leksi.E6dWebApp;
using Net.Leksi.E6dWebApp.Demo.Helloer;
using System.Diagnostics;

using Doer helloer = new();

NameHolder nameHolder = new() { Name = "World" };

helloer.Start();

IConnector connector = helloer.GetConnector();

Process.Start(new ProcessStartInfo
{
    FileName = connector.GetLink("/", nameHolder),
    UseShellExecute = true
});

while (true)
{
    Console.Write($"Hello {nameHolder.Name}! Another Name? ");
    nameHolder.Name = Console.ReadLine();
}
