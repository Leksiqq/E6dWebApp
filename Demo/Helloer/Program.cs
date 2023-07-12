using Net.Leksi.E6dWebApp;
using Net.Leksi.E6dWebApp.Demo.Helloer;

using Doer helloer = new();

NameHolder nameHolder = new() { Name = "World" };

helloer.Start();

IConnector connector = helloer.GetConnector();

Console.WriteLine($"Link: {connector.GetLink("/", nameHolder)}");

while (true)
{
    Console.Write($"Hello {nameHolder.Name}! Another Name? ");
    nameHolder.Name = Console.ReadLine();
}
