﻿using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using NermNermNerm.Stardew.LocalizeFromSource;
using Mono.Cecil;
using System.Text.Json;

namespace LocalizeFromSource
{
    /// <summary>
    ///   Combines user configuration with baseline configuration and domain configuration.
    /// </summary>
    public class CombinedConfig
    {
        private readonly IReadOnlySet<string> invariantMethodNames;
        private readonly Config userConfig;
        private readonly string projectPath;
        private Lazy<string?> gitHubUrlRoot;
        private Lazy<string?> gitRepoRootFolder;

        protected static readonly JsonSerializerOptions JsonReaderOptions = new JsonSerializerOptions()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };



        public CombinedConfig(AssemblyDefinition targetAssembly, string projectPath, Config userConfig)
        {
            // Someday this could deduce the project type from the assembly
            this.userConfig = userConfig;
            this.IsStrict = userConfig.IsStrict;
            this.projectPath = projectPath;

            this.TranslationCompiler = new SdvTranslationCompiler(this, projectPath);
            this.gitHubUrlRoot = new Lazy<string?>(this.GetGithubBaseUrl);
            this.gitRepoRootFolder = new Lazy<string?>(() => this.ExecuteGitCommand("rev-parse --show-toplevel"));
            this.invariantMethodNames = this.GetInvariantMethodNames(targetAssembly);
        }

        public TranslationCompiler TranslationCompiler { get; }

        public bool IsKnownInvariantString(string s) => this.TranslationCompiler.IsKnownInvariantString(s) || this.userConfig.InvariantStringPatterns.Any(m => m.IsMatch(s));

        public bool IsMethodWithInvariantArgs(string s) => this.invariantMethodNames.Contains(s);

        public bool IsStrict { get; }

        public bool ShouldIgnore(TypeDefinition typeDefinition)
        {
            // TODO: make this not sdv-specific or move something into SdvTranslationCompiler
            return typeDefinition.Name == "I18n" || typeDefinition.FullName.StartsWith("NermNermNerm.Stardew.LocalizeFromSource.");
        }

        public Uri? TryMakeGithubLink(string? fullPath, int? line)
        {
            var urlRoot = this.gitHubUrlRoot.Value;
            var repoRoot = this.gitRepoRootFolder.Value;
            if (fullPath is null || urlRoot is null || repoRoot is null)
            {
                return null;
            }

            var relativePath = Path.GetRelativePath(repoRoot, fullPath);
            if (relativePath.StartsWith(".."))
            {
                return null;
            }

            relativePath = relativePath.Replace('\\', '/');

            string fileUrl = $"{urlRoot}/{HttpUtility.UrlEncode(relativePath).Replace("%2f", "/")}";
            if (line.HasValue)
            {
                fileUrl += $"#L{line.Value}";
            }

            return new Uri(fileUrl);
        }

        private string? GetGithubBaseUrl()
        {
            string? repoRoot = this.ExecuteGitCommand("rev-parse --show-toplevel");
            if (repoRoot is null)
            {
                return null;
            }

            string? repoUrl = ExecuteGitCommand("remote get-url origin");
            if (repoUrl is null || !repoUrl.StartsWith("https://github.com/"))
            {
                return null;
            }

            string? branchName = this.GetDefaultBranch();
            if (branchName is null)
            {
                return null;
            }

            // Clean up the repository URL
            if (repoUrl.EndsWith(".git"))
            {
                repoUrl = repoUrl.Substring(0, repoUrl.Length - 4);
            }

            // Ensure the URL uses https instead of git protocol
            repoUrl = Regex.Replace(repoUrl, @"^git@github\.com:", "https://github.com/");

            // Construct the GitHub file URL
            return $"{repoUrl}/blob/{branchName}";
        }

        string? ExecuteGitCommand(string command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = command,
                WorkingDirectory = this.projectPath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output.Trim() : null;
            }
        }

        string? GetDefaultBranch()
        {
            // Shenanigans!  I see no way to deliver a really proper link.  There's no way to tell if the user has, at this
            //  point, pushed and all that, so we can't do, say, a permalink to a commit hash.  I feel like it's okay to be
            //  a little bit flakey here.  We're not betting the farm on these links being accurate anyway - we just need to
            //  get the translator close to the spot.  So we're just gonna go for the main branch and basically try and guess it.

            string? branches = this.ExecuteGitCommand("branch -r");
            if (branches is null)
            {
                return null;
            }

            var candidates = branches.Split('\n').Select(b => b.Trim()).Select(b => b.Replace("origin/", "")).ToHashSet();

            string[] defaultBranches = { "main", "master", "develop" };
            foreach (var branch in defaultBranches)
            {
                if (candidates.Contains(branch))
                {
                    return branch;
                }
            }

            // Fallback to the current branch if no default branch is found
            return ExecuteGitCommand("rev-parse --abbrev-ref HEAD");
        }

        private IReadOnlySet<string> GetInvariantMethodNames(AssemblyDefinition dll)
        {
            HashSet<string> invariantMethods = new();

            // .net standard things that are common enough to warrant a special place in our heart.
            invariantMethods.UnionWith(this.TranslationCompiler.DomainSpecificInvariantMethodNames);

            invariantMethods.UnionWith(GetMethodsWithCustomAttribute(dll));

            invariantMethods.UnionWith(this.userConfig.InvariantMethods);

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
