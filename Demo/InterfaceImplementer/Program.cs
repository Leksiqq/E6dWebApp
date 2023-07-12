using Net.Leksi.E6dWebApp.Demo.InterfaceImplementer;
using System.IO;
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

    foreach (Type type in asm.GetTypes())
    {
        if (type.IsInterface)
        {
            types.Add(type);
        }
    }

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
                generator.Implement(types[position - 1]);
                done = true;
            }
        }

    }
    else
    {
        Console.WriteLine($"The assembly : {path} does not contain any interface.");
        Console.WriteLine();
    }
}


