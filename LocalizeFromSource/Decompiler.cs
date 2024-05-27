using Mono.Cecil.Cil;
using Mono.Cecil;
using LocalizeFromSourceLib;
using System.Text.RegularExpressions;

namespace LocalizeFromSource
{
    public class Decompiler
    {
        public void FindLocalizableStrings(AssemblyDefinition assembly, Reporter reporter, IReadOnlySet<string> invariantMethods)
        {
            var t = new Decompiler();
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.HasBody)
                        {
                            t.FindLocalizableStrings(method, reporter, invariantMethods);
                        }
                    }

                    foreach (var property in type.Properties)
                    {
                        if (property.GetMethod?.HasBody == true)
                        {
                            t.FindLocalizableStrings(property.GetMethod, reporter, invariantMethods);
                        }
                        if (property.SetMethod?.HasBody == true)
                        {
                            t.FindLocalizableStrings(property.SetMethod, reporter, invariantMethods);
                        }
                        // TODO? What about the initializer?  Maybe it's part of a generated constructor?
                    }
                }
            }
        }

        public void FindLocalizableStrings(MethodDefinition method, Reporter reporter, IReadOnlySet<string> invariantMethods)
        {
            bool isNoStrictMode =
                method.CustomAttributes.Any(c => c.AttributeType.FullName == typeof(NoStrictAttribute).FullName)
                || method.DeclaringType.CustomAttributes.Any(c => c.AttributeType.FullName == typeof(NoStrictAttribute).FullName);
            var bestSequencePoint = method.DebugInformation.GetSequencePointMapping().Values.FirstOrDefault(); // If there are any, this is random.

            if ( /* method.Name == "ToString" && */ bestSequencePoint is null)
            {
                // Perhaps we should do this any time there is no sequence point information?  Seems like it would
                // always indicate generated code.
                return;
            }

            // LocalizeMethods.L is the one certainty in the storm.  Calls to it should always be immediately preceded by
            //   a ldstr instruction.  If they're not, it's a misuse of the function and will likely result in runtime errors
            //   so we first scan for that situation before proceeding with the more loosey-goosey world of invariant and
            //   formatted strings.
            var instructions = method.Body.Instructions;
            for (int pc = 1; pc < instructions.Count; ++pc)
            {
                var instruction = instructions[pc];
                var prevInstruction = instruction.Previous;
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? method.DebugInformation.GetSequencePoint(prevInstruction);
                if (this.IsCallToL(instruction) && prevInstruction.OpCode != OpCodes.Ldstr)
                {
                    reporter.ReportBadUsage(bestSequencePoint, TranslationCompiler.ImproperUseOfMethod, $"The argument to {nameof(LocalizeMethods)}.{nameof(LocalizeMethods.L)} should always be a literal string.  If this is a formatted string, use {nameof(LocalizeMethods.LF)} instead.");
                }
            }

            // TODO: There may be a similar pattern to protect LocalizeMethods.LF

            for (int pc = 0; pc < instructions.Count; ++pc)
            {
                var instruction = instructions[pc];
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;

                if (instruction.OpCode != OpCodes.Ldstr)
                {
                    continue;
                }

                // The idea here is to start from a 'Ldstr' instruction and proceed forward until we get to a 'call'
                //  instruction that we recognize - ignoring all the mayhem in between.  Because all the recognized
                //  methods are expecting a literal string, we can be somewhat confident that we're matching the
                //  string to the call correctly.  But we're totally dependent upon the code meeting this assumption.

                var ldStrInstruction = instruction;
                var ldStrSequencePoint = method.DebugInformation.GetSequencePoint(ldStrInstruction);
                string s = (string)instruction.Operand;

                ++pc;
                instruction = instructions[pc];
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                bool foundCall = false;
                while (pc < instructions.Count && instruction.OpCode != OpCodes.Ldstr)
                {
                    if (this.IsCallToL(instruction))
                    {
                        reporter.ReportLocalizedString(s, ldStrSequencePoint);
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToLF(instruction))
                    {
                        reporter.ReportLocalizedString(s, ldStrSequencePoint ?? bestSequencePoint);
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToInvariant(instruction, invariantMethods))
                    {
                        // Ignore it.
                        foundCall = true;
                        break;
                    }

                    ++pc;
                    if (pc < instructions.Count)
                    {
                        instruction = instructions[pc];
                        bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                    }
                }

                if (!foundCall) // Treat empty strings as invariant.
                {
                    if (!isNoStrictMode)
                    {
                        reporter.ReportBadString(s, ldStrSequencePoint ?? bestSequencePoint);
                    }

                    if (instruction.OpCode == OpCodes.Ldstr)
                    {
                        --pc; // back up one - the for() loop will increment this and skip the string at this instruction.
                    }
                }
            }
        }


        private bool IsCallToL(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(LocalizeMethods.L));

        private bool IsCallToInvariant(Instruction instruction, IReadOnlySet<string> invariantMethodNames)
            => (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                && instruction.Operand is MethodReference methodRef
                && invariantMethodNames.Contains(methodRef.DeclaringType.FullName + "." + methodRef.Name);

        private bool IsCallToLF(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(LocalizeMethods.LF));

        private bool IsCallToLocalizeMethods(Instruction instruction, string methodName)
            => instruction.OpCode == OpCodes.Call
                && instruction.Operand is MethodReference methodRef
                && methodRef.DeclaringType.FullName == typeof(LocalizeMethods).FullName
                && methodRef.Name == methodName;
    }
}
