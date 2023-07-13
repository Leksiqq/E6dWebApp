using Net.Leksi.E6dWebApp.Demo.InterfaceImplementer.Pages;
using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Net.Leksi.E6dWebApp.Demo.InterfaceImplementer;

public class Generator: Runner
{
    private readonly Regex _interfaceName = new Regex("^(I?)(.*)$");

    public TextReader Implement(Type @interface)
    {
        Start();

        IConnector connector = GetConnector();

        return connector.Get("/Implementation", @interface);

    }

    protected override void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();
        builder.Services.AddSingleton(this);
    }

    protected override void ConfigureApplication(WebApplication app)
    {
        app.MapRazorPages();
    }

    internal void Generate(ImplementationModel model)
    {
        Type @interface = (model.HttpContext.RequestServices.GetRequiredService<RequestParameter>()?.Parameter as Type)!;

        model.Contract = MakeTypeName(@interface);
        model.NamespaceValue = @interface.Namespace ?? string.Empty;

        Match match = _interfaceName.Match(@interface.Name);
        if (match.Success)
        {
            model.ClassName = $"{(string.IsNullOrEmpty(match.Groups[2].Value) ? match.Groups[1].Value : match.Groups[2].Value)}Poco";
        }

        model.Usings.Add(typeof(INotifyPropertyChanged).Namespace!);
        model.Usings.Add(typeof(PropertyChangedEventHandler).Namespace!);
        model.Usings.Add(typeof(PropertyChangedEventArgs).Namespace!);

        model.Interfaces.Add(model.Contract);
        model.Interfaces.Add(MakeTypeName(typeof(INotifyPropertyChanged)));

        NullabilityInfoContext nc = new();
        HashSet<string> variables = new();

        foreach (PropertyInfo pi in @interface.GetProperties())
        {
            PropertyModel pm = new()
            {
                Name = pi.Name,
                Type = MakeTypeName(pi.PropertyType),
            };
            if (nc.Create(pi).ReadState is NullabilityState.Nullable)
            {
                pm.Nullable = "?";
            }
            else if (string.IsNullOrEmpty(pm.Nullable) && pi.PropertyType.IsClass)
            {
                pm.Init = " = null!";
            }

            pm.FieldName = $"_{pi.Name.Substring(0, 1).ToLower()}{pi.Name.Substring(1)}";
            if (!variables.Add(pm.FieldName))
            {
                int i = 0;
                for(; !variables.Add(pm.FieldName + i); ++i) { }
                pm.FieldName += i;
            }
            model.Properties.Add(pm);
        }
    }

    private static string MakeTypeName(Type type)
    {
        if (type == typeof(void))
        {
            return "void";
        }
        if (!type.IsGenericType)
        {
            return type.Name;
        }
        if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return MakeTypeName(type.GetGenericArguments()[0]);
        }
        return type.GetGenericTypeDefinition().Name.Substring(0, type.GetGenericTypeDefinition().Name.IndexOf('`'))
            + '<' + String.Join(',', type.GetGenericArguments().Select(v => MakeTypeName(v))) + '>';
    }
}
