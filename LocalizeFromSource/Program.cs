using Mono.Cecil;
using Mono.Cecil.Cil;
using Spectre.Console.Cli;
using static LocalizeFromSourceLib.LocalizeMethods;

namespace LocalizeFromSource
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandApp<DecompileCommand>();
            app.Run(args);
        }

        public static void Test1()
        {
            Console.WriteLine(L("Hello, World!"));
            Console.WriteLine("Whoops");
        }
        public static void Test2()
        {
            int fiftySeven = 57;
            Console.WriteLine(LF($"I got {fiftySeven.ToString().Length} arguments"));
            Console.WriteLine($"error, I got {fiftySeven.ToString().Length} arguments");
        }
    }
}
