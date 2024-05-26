using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil.Cil;
using Mono.Cecil;
using LocalizeFromSourceLib;
using System.Text.RegularExpressions;

using static LocalizeFromSourceLib.LocalizeMethods;

namespace LocalizeFromSource
{
    internal class Decompiler
    {
        private static Regex formatRegex = new Regex(@"{\d+(:[^}]+)?}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void FindLocalizableStrings(string dllPath, Reporter reporter)
        {
            var t = new Decompiler();
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadSymbols = true });
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.HasBody)
                        {
                            t.FindLocalizableStrings(method, reporter);
                        }
                    }

                    foreach (var property in type.Properties)
                    {
                        if (property.GetMethod?.HasBody == true)
                        {
                            t.FindLocalizableStrings(property.GetMethod, reporter);
                        }
                        if (property.SetMethod?.HasBody == true)
                        {
                            t.FindLocalizableStrings(property.SetMethod, reporter);
                        }
                        // TODO? What about the initializer?  Maybe it's part of a generated constructor?
                    }
                }
            }
        }

        public void FindLocalizableStrings(MethodDefinition method, Reporter reporter)
        {
            int lastAppendLiteralErrorLineNumber = -1;
            var bestSequencePoint = method.DebugInformation.GetSequencePointMapping().Values.FirstOrDefault(); // If there are any, this is random.
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
                else if (this.IsCallToI(instruction))
                {
                    // Ignore it
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
                        if (this.IsCallToIF(instruction))
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
                    reporter.ReportBadString(s, ldStrSequencePoint ?? bestSequencePoint);
                }
            }
        }


        private static bool IsFormatString(string s) => formatRegex.IsMatch(s);


        private bool IsCallToL(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(LocalizeMethods.L));

        private bool IsCallToI(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(LocalizeMethods.I));

        private bool IsCallToLF(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(LocalizeMethods.LF));

        private bool IsCallToIF(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(LocalizeMethods.IF));

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
