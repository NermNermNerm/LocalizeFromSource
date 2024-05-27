using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Cecil;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizeFromSource
{
    public class DecompileCommand : Command<DecompileCommand.Settings>
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
            string configPath = Path.Combine(settings.SourceRoot, "LocalizeFromSourceConfig.json");
            Config? config = new Config();
            if (File.Exists(configPath))
            {
                try
                {
                    var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
                    options.Converters.Add(new RegexJsonConverter());
                    config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath), options);
                    if (config is null)
                    {
                        throw new JsonException("null is not expected");
                    }
                }
                catch(Exception ex)
                {
                    Console.Error.WriteLine($"error {TranslationCompiler.ErrorPrefix}{TranslationCompiler.BadConfigFile:d4}: {ex.Message}");
                    return 1;
                }
            }
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(settings.DllPath, new ReaderParameters { ReadSymbols = true });

            SdvTranslationCompiler compiler = new SdvTranslationCompiler();
            var decomp = new Decompiler();
            var reporter = new Reporter(config);
            decomp.FindLocalizableStrings(assembly, reporter, compiler.GetInvariantMethodNames(assembly, config));

            // Is this a good idea?  Should we really block the build for this?
            if (config.IsStrict && reporter.AnyUnmarkedStringsEncountered)
            {
                Console.Error.WriteLine("Not generating language JSON because there are strings in the source that need to be marked localizable or not.");
                return 1;
            }

            bool completedWithNoErrors = compiler.GenerateI18nFiles(settings.SourceRoot, false, reporter.LocalizableStrings);
            return completedWithNoErrors ? 0 : 1;
        }
    }
}
