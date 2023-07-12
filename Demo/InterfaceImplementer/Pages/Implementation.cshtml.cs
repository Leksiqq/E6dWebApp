using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Net.Leksi.E6dWebApp.Demo.InterfaceImplementer.Pages;

public class ImplementationModel : PageModel
{
    public void OnGet([FromServices]Generator generator)
    {
        generator.Generate(this);
    }
}
