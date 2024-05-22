using Mono.Cecil;
using Mono.Cecil.Cil;
using static LocalizeFromSourceLib.LocalizeMethods;

namespace LocalizeFromSource
{
    internal class Program
    {
        static void Main(string[] args)
        {
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly("LocalizeFromSource.dll");
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.HasBody)
                        {
                            foreach (var instruction in method.Body.Instructions)
                            {
                                if (instruction.OpCode == OpCodes.Call &&
                                    instruction.Operand is MethodReference methodRef &&
                                    methodRef.Name == "Loc2")
                                {
                                    var prevInstruction = instruction.Previous;
                                    if (prevInstruction.OpCode == OpCodes.Ldstr)
                                    {
                                        string arg = (string)prevInstruction.Operand;
                                        Console.WriteLine($"Method 'f' called with argument: {arg}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void Test1()
        {
            Console.WriteLine(L("Hello, World!"));
        }
        public static void Test2()
        {
            int fiftySeven = 57;
            Console.WriteLine(LI($"I got {fiftySeven.ToString().Length} arguments"));
        }
    }
}
