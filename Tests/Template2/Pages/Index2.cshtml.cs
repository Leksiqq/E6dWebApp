using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Requisitor;

namespace Template2.Pages
{
    public class IndexModel : PageModel
    {
        public string Message { get; set; }

        public IndexModel(Class2 requisitor)
        { 
            Message = requisitor.Message;
        }
    }
}