﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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

            [Description("If set, insists on every string specify whether it's invariant or localized.")]
            [CommandOption("--strict")]
            [DefaultValue(true)]
            public bool IsStrict { get; init; }

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
            var decomp = new Decompiler();
            decomp.FindLocalizableStrings(settings.DllPath);

            return 0;
        }
    }
}
