using Net.Leksi.E6dWebApp.Demo.InterfaceImplementer;
using System.Diagnostics;
using System.Reflection;

List<Type> types = new();
using Generator generator = new();

Assembly? asm = null;

bool done = false;

string? path = string.Empty;

while (!done)
{
    do
    {
        Console.WriteLine("Put path to assembly (nothing for current) and press Enter");
        Console.Write("> ");
        path = Console.ReadLine();

        asm = typeof(Generator).Assembly;

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                asm = Assembly.LoadFile(path!);
                break;
            }

            catch (Exception)
            {
                asm = null;
                Console.WriteLine($"Failed loading the assembly : {path}.");
                Console.WriteLine();
            }

        }

    }
    while (asm is null);

    types.Clear();

    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    foreach (Type type in asm.GetTypes())
    {
        if (type.IsInterface && !type.GetMethods().Any(m => !m.IsSpecialName))
        {
            types.Add(type);
        }
    }
    AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

    if (types.Any())
    {
        while (!done)
        {
            Console.WriteLine("Select one of these interfaces:");
            for (int i = 0; i < types.Count; ++i)
            {
                Console.WriteLine($"    {i + 1}) {types[i]}");
            }
            Console.WriteLine($"Put a number 1 to {types.Count} and press Enter");
            Console.Write("> ");

            string? number = Console.ReadLine();

            int position;

            if (string.IsNullOrEmpty(number) || !int.TryParse(number, out position) || position < 1 || position > types.Count)
            {
                Console.WriteLine($"Number is bad: {number}.");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"Implementing the interface: {types[position - 1]}");
                string filePath = Path.GetTempFileName();
                TextReader reader = generator.Implement(types[position - 1]);

                File.WriteAllText(filePath, reader.ReadToEnd());

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = filePath,
                    UseShellExecute = true
                });

                done = true;
            }
        }

    }
    else
    {
        Console.WriteLine($"The assembly : {path} does not contain any POCO interface.");
        Console.WriteLine();
    }
}

static Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
{
    string pathWithoutExtension = Path.Combine(
        Path.GetDirectoryName(args.RequestingAssembly!.Location)!,
        args.Name.Contains(',') ? args.Name.Substring(0, args.Name.IndexOf(",")) : args.Name
    );
    try
    {
        return Assembly.LoadFile(pathWithoutExtension + ".dll");
    }
    catch (ReflectionTypeLoadException)
    {
        return Assembly.LoadFile(pathWithoutExtension + ".exe");
    }
}


