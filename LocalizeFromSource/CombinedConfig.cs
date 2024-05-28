using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        public static CombinedConfig Create(AssemblyDefinition targetAssembly, string projectPath, Config userConfig)
        {
            // Someday this could deduce the project type from the assembly
            var compiler = new SdvTranslationCompiler();

            var methodNames = compiler.GetInvariantMethodNames(targetAssembly, userConfig);
            return new CombinedConfig(userConfig, methodNames, compiler);
        }

        private CombinedConfig(Config userConfig, IReadOnlySet<string> invariantMethodNames, TranslationCompiler compiler)
        {
            this.userConfig = userConfig;
            this.IsStrict = userConfig.IsStrict;
            this.TranslationCompiler = compiler;
            this.invariantMethodNames = invariantMethodNames;
        }

        public TranslationCompiler TranslationCompiler { get; }

        public bool IsKnownInvariantString(string s) => !hasAnyLettersInIt.IsMatch(s) || this.userConfig.InvariantStringPatterns.Any(m => m.IsMatch(s));

        public bool IsMethodWithInvariantArgs(string s) => this.invariantMethodNames.Contains(s);

        public bool IsStrict { get; }
    }
}
