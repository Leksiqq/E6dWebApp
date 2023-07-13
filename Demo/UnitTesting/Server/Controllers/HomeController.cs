using Microsoft.AspNetCore.Mvc;

namespace Net.Leksi.E6dWebApp.Demo.UnitTesting.Controllers;

public class HomeController : Controller
{
    [Route("/{id=0}")]
    [HttpGet]
    public async Task GetReversedString(int id)
    {
        if(HttpContext.RequestServices.GetRequiredService<IStringProvider>().Get(id) is string str)
        {
            HttpContext.Response.StatusCode = 200;
            for (int i = str.Length - 1; i >= 0; --i)
            {
                await HttpContext.Response.WriteAsync(new String(new char[] { str[i] }));
            }
        }
        else
        {
            HttpContext.Response.StatusCode = 404;
            await HttpContext.Response.WriteAsync("Not Found");
        }
    }

}
