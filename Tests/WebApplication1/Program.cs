using Net.Leksi.E6dWebApp;
using WebApplication1;

using Server server = new();

server.Start();

IConnector connector = server.GetConnector();

string oneTimeUrl = connector.GetOneTimeUrl("/");

Console.WriteLine(oneTimeUrl);

Console.ReadLine();