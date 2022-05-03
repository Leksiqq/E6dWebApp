using Microsoft.AspNetCore.Mvc.RazorPages;
using Requisitor;

namespace Template3.Pages
{
    public class IndexModel : PageModel
    {
        public string Message { get; set; }

        public IndexModel(Class1 requisitor)
        {
            Message = $"3: {requisitor.Message}";
        }

    }
}