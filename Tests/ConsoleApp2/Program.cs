using Net.Leksi.DocsRazorator;
using Requisitor;

DocsRazorator dr = new DocsRazorator();

var requisitor1 = new Class1();
var requisitor2 = new Class2();

await foreach (KeyValuePair<string, string> content in dr.Generate(
    new object[] { 
        requisitor1, 
        requisitor2, 
        typeof(Template1.Pages.IndexModel).Assembly,
        typeof(Template2.Pages.IndexModel).Assembly
    }, 
    new[] { "Index1", "Index2" }))
{
    Console.WriteLine($"-------- {content.Key} --------");
    Console.WriteLine(content.Value);

}