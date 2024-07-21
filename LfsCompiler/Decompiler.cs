using Mono.Cecil.Cil;
using Mono.Cecil;
using NermNermNerm.Stardew.LocalizeFromSource;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace LocalizeFromSource
{
    public class Decompiler
    {
        private record MethodRunTimeToCompileTimeMapping(bool isFormat, Func<string, IEnumerable<string>> decompile);

        private readonly CombinedConfig config;
        private readonly Dictionary<string, MethodRunTimeToCompileTimeMapping> methodMapping;

        // This class has a mess of hard-coded references to SdvLocalizeMethods.  If this gets split out
        //  and used in other contexts, perhaps it could take a type-argument and replace all those hard-coded
        //  references with Reflection-based references.

        public Decompiler(CombinedConfig config, Type compileTimeMethods, Type runTimeMethods)
        {
            this.config = config;
            this.methodMapping = new();

            foreach (var runTimeMethod in runTimeMethods.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var runTimeParameters = runTimeMethod.GetParameters();
                if (runTimeMethod.GetGenericArguments().Length > 0 || runTimeParameters.Length != 1
                    || (runTimeParameters[0].ParameterType != typeof(string) && runTimeParameters[0].ParameterType != typeof(FormattableString)))
                {
                    // not one of ours.  Maybe there should be a better way to more clearly mark this?
                    continue;
                }

                var compileTimeMethod = compileTimeMethods.GetMethod(runTimeMethod.Name, BindingFlags.Public | BindingFlags.Static);
                if (compileTimeMethod is null)
                {
                    // This is a code fault, not a user error.
                    throw new InvalidOperationException($"{compileTimeMethods.Name} should have an implementation for {runTimeMethod.Name}.");
                }

                var compileTimeParameters = compileTimeMethod.GetParameters();
                if (compileTimeParameters.Length != 1 || compileTimeParameters[0].ParameterType != typeof(string))
                {
                    throw new InvalidOperationException($"{compileTimeMethods.FullName}.{compileTimeMethod.Name} should take a single parameter of type string.");
                }

                if (compileTimeMethod.ReturnType != typeof(IEnumerable<string>))
                {
                    throw new InvalidOperationException($"{compileTimeMethods.FullName}.{compileTimeMethod.Name} should return IEnumerable<string>.");
                }

                methodMapping.Add($"{runTimeMethods.FullName}.{runTimeMethod.Name}",
                    new MethodRunTimeToCompileTimeMapping(
                        runTimeParameters[0].ParameterType == typeof(FormattableString),
                        s => (IEnumerable<string>)compileTimeMethod.Invoke(null, [s])!));
            }
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
            var lastSequencePointSeen = method.DebugInformation.GetSequencePointMapping().Values.FirstOrDefault(); // If there are any, this is random.

            if ( /* method.Name == "ToString" && */ lastSequencePointSeen is null)
            {
                // This condition seems to indicate generated code - like code generated for record ToString methods
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
                lastSequencePointSeen = method.DebugInformation.GetSequencePoint(instruction) ?? lastSequencePointSeen;
                if (instruction.OpCode == OpCodes.Call
                    && instruction.Operand is MethodReference methodRef
                    && this.methodMapping.TryGetValue($"{methodRef.DeclaringType.FullName}.{methodRef.Name}", out var mapping)
                    && !mapping.isFormat
                    && prevInstruction.OpCode != OpCodes.Ldstr)
                {
                    string messagePostFix = "";
                    string fullName = $"{methodRef.DeclaringType.FullName}.{methodRef.Name}";
                    if (this.methodMapping.TryGetValue(fullName + "F", out var formatVariant) && formatVariant.isFormat)
                    {
                        messagePostFix += $"  If this is a formatted string, perhaps you should be using {methodRef.Name}F?";
                    }
                    reporter.ReportImproperUseOfMethod(lastSequencePointSeen, $"The argument to {fullName} should always be a literal string." + messagePostFix);
                }
            }

            lastSequencePointSeen = method.DebugInformation.GetSequencePointMapping().Values.FirstOrDefault();
            for (int pc = 0; pc < instructions.Count; ++pc)
            {
                var instruction = instructions[pc];
                lastSequencePointSeen = method.DebugInformation.GetSequencePoint(instruction) ?? lastSequencePointSeen;

                if (!this.IsLdStrInstruction(instruction, out string? s))
                {
                    continue;
                }

                var ldStrSequencePoint = method.DebugInformation.GetSequencePoint(instruction);
                var lastSequencePointBeforeLdStr = lastSequencePointSeen;
                var lastSequencePointSinceLdStr = ldStrSequencePoint;

                // The idea here is to start from a 'Ldstr' instruction and proceed forward until we get to a 'call'
                //  instruction that we recognize - ignoring all the mayhem in between.  Because all the recognized
                //  methods are expecting a literal string, we can be somewhat confident that we're matching the
                //  string to the call correctly.  But we're totally dependent upon the code meeting this assumption.

                var ldStrInstruction = instruction;

                ++pc;
                instruction = instructions[pc];
                // bestSequencePoint is, in decreasing order of priority:
                //   The sequence point of the ldStr instruction
                //   The sequence point 
                lastSequencePointSeen = method.DebugInformation.GetSequencePoint(instruction) ?? lastSequencePointSeen;
                bool foundCall = false;
                while (pc < instructions.Count && !this.IsLdStrInstruction(instruction, out _))
                {
                    if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt || instruction.OpCode == OpCodes.Newobj)
                        && instruction.Operand is MethodReference methodRef)
                    {
                        var methodFullName = $"{methodRef.DeclaringType.FullName}.{methodRef.Name}";
                        if (this.methodMapping.TryGetValue(methodFullName, out var mapping))
                        {
                            foreach (var subString in mapping.decompile(s))
                            {
                                reporter.ReportLocalizedString(subString, ldStrSequencePoint ?? lastSequencePointSinceLdStr ?? lastSequencePointBeforeLdStr);
                            }

                            foundCall = true;
                            break;
                        }
                        else if (config.IsMethodWithInvariantArgs(methodFullName))
                        {
                            foundCall = true;
                            break;
                        }
                    }

                    ++pc;
                    if (pc < instructions.Count)
                    {
                        instruction = instructions[pc];
                        lastSequencePointSinceLdStr = method.DebugInformation.GetSequencePoint(instruction) ?? lastSequencePointSinceLdStr;
                    }
                }

                if (!foundCall) // Treat empty strings as invariant.
                {
                    if (!isNoStrictMode && this.config.IsStrict)
                    {
                        reporter.ReportUnmarkedString(s, ldStrSequencePoint ?? lastSequencePointBeforeLdStr);
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
                if ((instruction.Next is not null && this.IsCallToNonFormat(instruction.Next)) || (s != "" && !this.config.IsKnownInvariantString(s)))
                {
                    loadedString = s;
                    return true;
                }
            }

            loadedString = null;
            return false;
        }

        private bool IsCallToNonFormat(Instruction instruction)
            => instruction.OpCode == OpCodes.Call
                && instruction.Operand is MethodReference methodRef
                && this.methodMapping.TryGetValue($"{methodRef.DeclaringType.FullName}.{methodRef.Name}", out var mapping)
                && !mapping.isFormat;
    }
}
