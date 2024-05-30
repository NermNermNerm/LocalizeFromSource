using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LocalizeFromSourceLib;
using Mono.Cecil;

namespace LocalizeFromSource
{
    /// <summary>
    ///   Combines user configuration with baseline configuration and domain configuration.
    /// </summary>
    public class CombinedConfig
    {
        private readonly IReadOnlySet<string> invariantMethodNames;
        private readonly Config userConfig;
        private readonly Regex hasAnyLettersInIt = new Regex(@"\p{L}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly string projectPath;

        public static CombinedConfig Create(AssemblyDefinition targetAssembly, string projectPath, Config userConfig)
        {
            // Someday this could deduce the project type from the assembly

            var methodNames = GetInvariantMethodNames(targetAssembly, userConfig);
            return new CombinedConfig(userConfig, methodNames, projectPath);
        }


        private CombinedConfig(Config userConfig, IReadOnlySet<string> invariantMethodNames, string projectPath)
        {
            this.userConfig = userConfig;
            this.IsStrict = userConfig.IsStrict;
            this.invariantMethodNames = invariantMethodNames;
            this.projectPath = projectPath;

            this.TranslationCompiler = new SdvTranslationCompiler(this);
        }

        public TranslationCompiler TranslationCompiler { get; }

        public bool IsKnownInvariantString(string s) => !hasAnyLettersInIt.IsMatch(s) || this.userConfig.InvariantStringPatterns.Any(m => m.IsMatch(s));

        public bool IsMethodWithInvariantArgs(string s) => this.invariantMethodNames.Contains(s);

        public bool IsStrict { get; }

        public bool ShouldIgnore(TypeDefinition typeDefinition)
        {
            // TODO: make this not sdv-specific or move something into SdvTranslationCompiler
            return typeDefinition.Name == "I18n";
        }

        public string MakeRelative(string fullPath)
        {
            return Path.GetRelativePath(this.projectPath, fullPath);
        }

        private static IReadOnlySet<string> GetInvariantMethodNames(AssemblyDefinition dll, Config config)
        {
            HashSet<string> invariantMethods =
            [
                typeof(LocalizeMethods).FullName + "." + nameof(LocalizeMethods.I),
                typeof(LocalizeMethods).FullName + "." + nameof(LocalizeMethods.IF),
            ];

            // .net standard things that are common enough to warrant a special place in our heart.
            invariantMethods.UnionWith(new string[]
            {
                "System.Text.RegularExpressions.Regex..ctor"
            });

            invariantMethods.UnionWith(SdvLocalizations.DomainSpecificInvariantMethodNames);

            invariantMethods.UnionWith(GetMethodsWithCustomAttribute(dll));

            invariantMethods.UnionWith(config.InvariantMethods);

            return invariantMethods;
        }

        private static IEnumerable<string> GetMethodsWithCustomAttribute(AssemblyDefinition assembly)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.CustomAttributes.Any(c => c.AttributeType.FullName == typeof(LocalizeFromSourceLib.ArgumentIsCultureInvariantAttribute).FullName))
                        {
                            yield return $"{method.DeclaringType.FullName}.{method.Name}";
                        }
                    }
                }
            }
        }

    }
}
