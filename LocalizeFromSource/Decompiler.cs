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

        public void FindLocalizableStrings(string dllPath)
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
                            t.FindLocalizableStrings(method);
                        }
                    }

                    foreach (var property in type.Properties)
                    {
                        if (property.GetMethod?.HasBody == true)
                        {
                            t.FindLocalizableStrings(property.GetMethod);
                        }
                        if (property.SetMethod?.HasBody == true)
                        {
                            t.FindLocalizableStrings(property.SetMethod);
                        }
                        // TODO? What about the initializer?  Maybe it's part of a generated constructor?
                    }
                }
            }
        }

        public void FindLocalizableStrings(MethodDefinition method)
        {
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
                    this.ReportLocalizedString(s, ldStrSequencePoint);
                }
                else if (this.IsCallToI(instruction))
                {
                    // Ignore it
                }
                else if (this.IsCallToAppendLiteral(instruction))
                {
                    ReportBadFormatString(s, ldStrSequencePoint ?? bestSequencePoint);
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
                            this.ReportLocalizedString(s, ldStrSequencePoint ?? bestSequencePoint);
                            foundCall = true;
                            break;
                        }
                        ++pc;
                        instruction = instructions[pc];
                        bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                    };

                    if (!foundCall)
                    {
                        ReportBadFormatString(s, ldStrSequencePoint ?? bestSequencePoint);
                        --pc; // We might be sitting on a Ldstr instruction
                    }
                }
                else
                {
                    this.ReportBadString(s, ldStrSequencePoint ?? bestSequencePoint);
                }
            }
        }

        private string GetPositionString(SequencePoint? sequencePoint)
            => sequencePoint is null ? "no-debug-info" : $"{sequencePoint.Document.Url}({sequencePoint.StartLine}, {sequencePoint.StartColumn})";

        protected virtual void ReportBadString(string s, SequencePoint? sequencePoint)
        {
            Console.Error.WriteLine(
                IF($"{GetPositionString(sequencePoint)}: {TranslationCompiler.ErrorPrefix}{TranslationCompiler.StringNotMarked:d4}: ")
                + LF($"String is not marked as invariant or localized - it should be surrounded with I() or L() to indicate which it is: \"{s}\""));
        }

        protected virtual void ReportBadFormatString(string s, SequencePoint? sequencePoint)
        {
            Console.Error.WriteLine(
                IF($"{GetPositionString(sequencePoint)}: {TranslationCompiler.ErrorPrefix}{TranslationCompiler.StringNotMarked:d4}: ")
                + LF($"Formatted string is not marked as invariant or localized - it should be surrounded with IF() or LF() to indicate which it is: \"{s}\""));
        }

        protected virtual void ReportLocalizedString(string s, SequencePoint? sequencePoint)
        {
            Console.WriteLine($"Found '{s}' at line {sequencePoint?.StartLine} of {sequencePoint?.Document.Url}");
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
