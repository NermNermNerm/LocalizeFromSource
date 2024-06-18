using System.ComponentModel;
using System.Text.Json;
using Mono.Cecil;
using NermNermNerm.Stardew.LocalizeFromSource;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    [Description("Finds marked localized strings within a DLL and builds translation files for them.")]
    public class BuildI18nCommand : Command<BuildI18nCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [Description("Path to the compiled DLL to be localized. (Required)")]
            [CommandOption("-d|--dll")]
            public string DllPath { get; init; } = null!;

            [Description("Path to the root of the source tree for the project aka the directory containing the .csproj file. (Required)")]
            [CommandOption("-p|--sourceRoot")]
            public string SourceRoot { get; init; } = null!;

            public override ValidationResult Validate()
            {
                if (this.DllPath is null)
                {
                    return ValidationResult.Error("The --dll parameter must be supplied.");
                }
                if (!File.Exists(this.DllPath))
                {
                    return ValidationResult.Error($"The path specified with --dll does not exist: {this.DllPath}");
                }

                if (this.SourceRoot is null)
                {
                    return ValidationResult.Error("The --sourceRoot parameter must be supplied.");
                }
                if (!Directory.Exists(this.SourceRoot))
                {
                    return ValidationResult.Error($"The directory specified with --sourceRoot does not exist: {this.SourceRoot}");
                }

                return ValidationResult.Success();
            }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            var userConfig = Config.ReadFromFile(settings.SourceRoot);
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(settings.DllPath, new ReaderParameters { ReadSymbols = true });

            var combinedConfig = new CombinedConfig(GetMethodsWithCustomAttribute(assembly), settings.SourceRoot, userConfig);

            var decomp = new Decompiler(combinedConfig, typeof(SdvLocalizeCompiler), typeof(SdvLocalize));
            var reporter = new Reporter(userConfig);
            decomp.FindLocalizableStrings(assembly, reporter);

            // Is this a good idea?  Should we really block the build for this?
            if (userConfig.IsStrict && reporter.AnyUnmarkedStringsEncountered)
            {
                Console.Error.WriteLine("Not generating language JSON because there are strings in the source that need to be marked localizable or not.");
                return 1;
            }

            bool completedWithNoErrors = combinedConfig.TranslationCompiler.GenerateI18nFiles(reporter.LocalizableStrings);
            return completedWithNoErrors ? 0 : 1;
        }

        public static IEnumerable<string> GetMethodsWithCustomAttribute(AssemblyDefinition assembly)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        if (method.CustomAttributes.Any(c => c.AttributeType.FullName == typeof(ArgumentIsCultureInvariantAttribute).FullName))
                        {
                            yield return $"{method.DeclaringType.FullName}.{method.Name}";
                        }
                    }
                }
            }
        }


    }
}
