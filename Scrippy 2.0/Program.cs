using System.Text;

namespace Scrippy2;

public class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8; //goes to \uD7A3
        Console.InputEncoding = Encoding.UTF8;

        string filename;

#if DEBUG
        filename = "Test.sp";
#else
        if (args.Length == 0)
        {
            Console.Write("Enter path to source file: ");
            filename = Console.ReadLine() ?? "";
        }
        else if (args.Length == 1) { filename = args[0]; }
        else { Console.Write("Provide only 1 source file"); return; }
#endif

        //validate file
        if (!filename.EndsWith(".sp")) { Console.WriteLine($"File {filename} is not a Scrippy source file (.sp)"); return; }
        if (!File.Exists(filename)) { Console.WriteLine($"File {filename} does not exist"); return; }

        //test
        string source = File.ReadAllText(filename);
        Console.WriteLine(source);

    }
}
