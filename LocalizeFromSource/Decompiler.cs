using Mono.Cecil.Cil;
using Mono.Cecil;
using LocalizeFromSourceLib;
using System.Text.RegularExpressions;

namespace LocalizeFromSource
{
    public class Decompiler
    {
        private static Regex formatRegex = new Regex(@"{\d+(:[^}]+)?}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            int lastAppendLiteralErrorLineNumber = -1;
            var bestSequencePoint = method.DebugInformation.GetSequencePointMapping().Values.FirstOrDefault(); // If there are any, this is random.

            if ( /* method.Name == "ToString" && */ bestSequencePoint is null)
            {
                // Perhaps we should do this any time there is no sequence point information?  Seems like it would
                // always indicate generated code.
                return;
            }

            var instructions = method.Body.Instructions;
            for (int pc = 0; pc < instructions.Count; ++pc)
            {
                var instruction = instructions[pc];
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;

                if (instruction.OpCode != OpCodes.Ldstr)
                {
                    continue;
                }

                var ldStrInstruction = instruction;
                var ldStrSequencePoint = method.DebugInformation.GetSequencePoint(ldStrInstruction);
                string s = (string)instruction.Operand;
                ++pc;
                instruction = instructions[pc];
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                if (this.IsCallToL(instruction))
                {
                    reporter.ReportLocalizedString(s, ldStrSequencePoint);
                }
                else if (this.IsCallToAppendLiteral(instruction))
                {
                    // Format strings like $"foo {x} bar" are often converted to
                    //  inlined instructions, rather than "foo {0} bar" and then passed
                    //  to the formatter.  That means that we see "foo " and " bar"
                    //  as distinct strings.  To reduce the noise, we're just going to
                    //  report a single one per line.  Note that this means we report
                    //  a single instance of the error even if there are actually two
                    //  format strings on the line.  It seems a price worth paying.
                    var sp = ldStrSequencePoint ?? bestSequencePoint;
                    if (sp is not null && sp.StartLine != lastAppendLiteralErrorLineNumber)
                    {
                        reporter.ReportBadFormatString(s, sp);
                        lastAppendLiteralErrorLineNumber = sp.StartLine;
                    }
                }
                else if (IsFormatString(s))
                {
                    bool foundCall = false;
                    while (pc < instructions.Count && instruction.OpCode != OpCodes.Ldstr)
                    {
                        if (this.IsCallToInvariant(instruction, invariantMethods))
                        {
                            // Ignore it.
                            foundCall = true;
                            break;
                        }
                        else if (this.IsCallToLF(instruction))
                        {
                            reporter.ReportLocalizedString(s, ldStrSequencePoint ?? bestSequencePoint);
                            foundCall = true;
                            break;
                        }
                        ++pc;
                        instruction = instructions[pc];
                        bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                    };

                    if (!foundCall)
                    {
                        reporter.ReportBadFormatString(s, ldStrSequencePoint ?? bestSequencePoint);
                        --pc; // We might be sitting on a Ldstr instruction
                    }
                }
                else
                {
                    while (pc < instructions.Count && instruction.OpCode != OpCodes.Ldstr && instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                    {
                        ++pc;
                        instruction = instructions[pc];
                        bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                    }
                    if (!this.IsCallToInvariant(instruction, invariantMethods))
                    {
                        reporter.ReportBadString(s, ldStrSequencePoint ?? bestSequencePoint);
                    }

                    if (instruction.OpCode == OpCodes.Ldstr)
                    {
                        --pc;
                    }

                }
            }
        }


        private static bool IsFormatString(string s) => formatRegex.IsMatch(s);


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

        private bool IsCallToAppendLiteral(Instruction instruction)
            => instruction.OpCode == OpCodes.Call
                && instruction.Operand is MethodReference methodRef
                && methodRef.DeclaringType.FullName == typeof(System.Runtime.CompilerServices.DefaultInterpolatedStringHandler).FullName
                && methodRef.Name == "AppendLiteral";
    }
}
