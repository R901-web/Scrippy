using System;
using System.IO;
using System.Text;

namespace Scrippy
{
    public static class Program
    {
        public static string[] lines { get; private set; }

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8; //goes to \uD7A3
            Console.InputEncoding = Encoding.UTF8;

            //get filename
            string filename;
#if DEBUG
            filename = "Test.sp";
#else
            if (args.Length == 0)
            {
                Console.Write("Enter path to source file: ");
                filename = Console.ReadLine();
            }
            else if (args.Length == 1) { filename = args[0]; }
            else { Console.Write("Provide only 1 source file"); return; }

            //check if file valid
            if (!filename.EndsWith(".sp") && !filename.EndsWith(".scrippy")) { Console.WriteLine($"File {filename} is not a Scrippy source file (.sp)"); return; }
            if (!File.Exists(filename)) { Console.WriteLine($"File {filename} does not exist"); return; }
#endif

            //begin lexing
            lines = File.ReadAllText(filename).Split('\n');

            Lexer l = new Lexer(File.ReadAllText(filename));
            Token[] tokens = l.lexChars();
            if (DiagnosticHandler.hadError)
            {
                DiagnosticHandler.reportAll();
                return;
            }
            foreach (Token t in tokens) { Console.WriteLine(DebugTools.stringify(t)); }
            if (DiagnosticHandler.hadWarning) { DiagnosticHandler.reportAll(); DiagnosticHandler.clear(); }

            Console.WriteLine();

            Parser p = new Parser(tokens);
            Stmt[] program = p.parseTokens();

            foreach (Stmt s in program) { Console.WriteLine(DebugTools.stringify(s)); }

            if (DiagnosticHandler.hadError)
            {
                DiagnosticHandler.reportAll();
                return;
            }
            if (DiagnosticHandler.hadWarning) { DiagnosticHandler.reportAll(); DiagnosticHandler.clear(); }

            Console.WriteLine();

            /*
            Resolver r = new Resolver(program);
            r.resolveAST();

            if (DiagnosticHandler.hadError)
            {
                DiagnosticHandler.reportAll();
                return;
            }
            if (DiagnosticHandler.hadWarning) { DiagnosticHandler.reportAll(); DiagnosticHandler.clear(); }
            */

            Interpreter i = new Interpreter(program);
            i.interpretAST();
        }
    }
}
