using Mono.Cecil.Cil;
using Mono.Cecil;
using NermNermNerm.Stardew.LocalizeFromSource;
using System.Diagnostics.CodeAnalysis;

namespace LocalizeFromSource
{
    public class Decompiler
    {
        private readonly CombinedConfig config;

        // This class has a mess of hard-coded references to SdvLocalizeMethods.  If this gets split out
        //  and used in other contexts, perhaps it could take a type-argument and replace all those hard-coded
        //  references with Reflection-based references.

        public Decompiler(CombinedConfig config)
        {
            this.config = config;
        }

        public void FindLocalizableStrings(AssemblyDefinition assembly, Reporter reporter)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types.Where(t => !this.config.ShouldIgnore(t)))
                {
                    this.FindLocalizableStrings(type, reporter);
                }
            }
        }

        public void FindLocalizableStrings(TypeDefinition type, Reporter reporter)
        {
            foreach (var method in type.Methods)
            {
                if (method.HasBody)
                {
                    this.FindLocalizableStrings(method, reporter);
                }
            }

            foreach (var property in type.Properties)
            {
                if (property.GetMethod?.HasBody == true)
                {
                    this.FindLocalizableStrings(property.GetMethod, reporter);
                }
                if (property.SetMethod?.HasBody == true)
                {
                    this.FindLocalizableStrings(property.SetMethod, reporter);
                }
            }

            foreach (var nestedType in type.NestedTypes)
            {
                this.FindLocalizableStrings(nestedType, reporter);
            }
        }

        public void FindLocalizableStrings(MethodDefinition method, Reporter reporter)
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
                    reporter.ReportBadUsage(bestSequencePoint, TranslationCompiler.ImproperUseOfMethod, $"The argument to {nameof(SdvLocalizeMethods)}.{nameof(SdvLocalizeMethods.L)} should always be a literal string.  If this is a formatted string, use {nameof(SdvLocalizeMethods.LF)} instead.");
                }
            }

            // TODO: There may be a similar pattern to protect LocalizeMethods.LF

            for (int pc = 0; pc < instructions.Count; ++pc)
            {
                var instruction = instructions[pc];
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;

                if (!this.IsLdStrInstruction(instruction, out string? s))
                {
                    continue;
                }

                // The idea here is to start from a 'Ldstr' instruction and proceed forward until we get to a 'call'
                //  instruction that we recognize - ignoring all the mayhem in between.  Because all the recognized
                //  methods are expecting a literal string, we can be somewhat confident that we're matching the
                //  string to the call correctly.  But we're totally dependent upon the code meeting this assumption.

                var ldStrInstruction = instruction;
                var ldStrSequencePoint = method.DebugInformation.GetSequencePoint(ldStrInstruction);

                ++pc;
                instruction = instructions[pc];
                bestSequencePoint = method.DebugInformation.GetSequencePoint(instruction) ?? bestSequencePoint;
                bool foundCall = false;
                while (pc < instructions.Count && !this.IsLdStrInstruction(instruction, out _))
                {
                    if (this.IsCallToL(instruction))
                    {
                        reporter.ReportLocalizedString(s, ldStrSequencePoint ?? bestSequencePoint);
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToLF(instruction))
                    {
                        reporter.ReportLocalizedString(SdvTranslator.TransformCSharpFormatStringToSdvFormatString(s), ldStrSequencePoint ?? bestSequencePoint);
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToSdvEvent(instruction))
                    {
                        foreach (string localizableSegment in SdvLocalizations.SdvEvent(s))
                        {
                            reporter.ReportLocalizedString(localizableSegment, ldStrSequencePoint ?? bestSequencePoint);
                        }
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToSdvQuest(instruction))
                    {
                        foreach (string localizableSegment in SdvLocalizations.SdvQuest(s))
                        {
                            reporter.ReportLocalizedString(localizableSegment, ldStrSequencePoint ?? bestSequencePoint);
                        }
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToSdvMail(instruction))
                    {
                        foreach (string localizableSegment in SdvLocalizations.SdvMail(s))
                        {
                            reporter.ReportLocalizedString(localizableSegment, ldStrSequencePoint ?? bestSequencePoint);
                        }
                        foundCall = true;
                        break;
                    }
                    else if (this.IsCallToInvariant(instruction))
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
                    if (!isNoStrictMode && this.config.IsStrict)
                    {
                        reporter.ReportBadString(s, ldStrSequencePoint ?? bestSequencePoint);
                    }

                    if (this.IsLdStrInstruction(instruction, out _))
                    {
                        --pc; // back up one - the for() loop will increment this and skip the string at this instruction.
                    }
                }
            }
        }


        private bool IsLdStrInstruction(Instruction instruction, [NotNullWhen(true)] out string? loadedString)
        {
            if (instruction.OpCode == OpCodes.Ldstr)
            {
                string s = (string)instruction.Operand;
                // If the next instruction is L() or this string doesn't match a known invariant pattern
                if ((instruction.Next is not null && this.IsCallToL(instruction.Next)) || (s != "" && !this.config.IsKnownInvariantString(s)))
                {
                    loadedString = s;
                    return true;
                }
            }

            loadedString = null;
            return false;
        }

        private bool IsCallToL(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(SdvLocalizeMethods.L));

        private bool IsCallToSdvEvent(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(SdvLocalizeMethods.SdvEvent));

        private bool IsCallToSdvMail(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(SdvLocalizeMethods.SdvMail));

        private bool IsCallToSdvQuest(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(SdvLocalizeMethods.SdvQuest));

        private bool IsCallToInvariant(Instruction instruction)
            => (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt || instruction.OpCode == OpCodes.Newobj)
                && instruction.Operand is MethodReference methodRef
                && this.config.IsMethodWithInvariantArgs(methodRef.DeclaringType.FullName + "." + methodRef.Name);

        private bool IsCallToLF(Instruction instruction)
            => this.IsCallToLocalizeMethods(instruction, nameof(SdvLocalizeMethods.LF));

        private bool IsCallToLocalizeMethods(Instruction instruction, string methodName)
            => instruction.OpCode == OpCodes.Call
                && instruction.Operand is MethodReference methodRef
                && methodRef.DeclaringType.FullName == typeof(SdvLocalizeMethods).FullName
                && methodRef.Name == methodName;
    }
}
