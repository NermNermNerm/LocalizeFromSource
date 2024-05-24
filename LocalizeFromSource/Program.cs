using Mono.Cecil;
using Mono.Cecil.Cil;
using static LocalizeFromSourceLib.LocalizeMethods;

namespace LocalizeFromSource
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var t = new Decompiler();
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly("LocalizeFromSource.dll", new ReaderParameters { ReadSymbols = true });
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types.Where(t => t.Name == "Program"))
                {
                    foreach (var method in type.Methods.Where(m => m.Name == "Test1" || m.Name == "Test2"))
                    {
                        if (method.HasBody)
                        {
                            t.FindLocalizableStrings(method);
                        }
                    }
                }
            }
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
