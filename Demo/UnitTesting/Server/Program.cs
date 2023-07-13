using Net.Leksi.E6dWebApp.Demo.UnitTesting;

Server server = new();
var builder = WebApplication.CreateBuilder(args);
server.ConfigureBuilder(builder);
var app = builder.Build();
server.ConfigureApplication(app);
app.Run();
