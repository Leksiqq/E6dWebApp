using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Requisitor;

namespace Template1.Pages
{
    public class IndexModel : PageModel
    {
        public string Message { get; set; }

        public IndexModel(Class1 requisitor)
        {
            Message = requisitor.Message;
        }

    }
}