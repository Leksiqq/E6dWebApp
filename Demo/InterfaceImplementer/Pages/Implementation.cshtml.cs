using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Net.Leksi.E6dWebApp.Demo.InterfaceImplementer.Pages;

public class ImplementationModel : PageModel
{
    internal string ClassName { get; set; } = null!;
    internal string NamespaceValue { get; set; } = null!;
    internal string Contract { get; set; } = null!;
    internal HashSet<string> Usings { get; init; } = new();
    internal List<string> Interfaces { get; init; } = new();
    internal List<PropertyModel> Properties { get; init; } = new();


    public void OnGet([FromServices]Generator generator)
    {
        generator.Generate(this);
    }
}
