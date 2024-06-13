using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandApp<DecompileCommand>();
            app.Run(args);
        }
    }
}
