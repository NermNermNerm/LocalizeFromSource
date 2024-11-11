using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using NermNermNerm.Stardew.LocalizeFromSource;
using Mono.Cecil;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text;
using LfsCompiler;

namespace LocalizeFromSource
{
    /// <summary>
    ///   Combines user configuration with baseline configuration and domain configuration.
    /// </summary>
    public class CombinedConfig
    {
        private readonly IReadOnlySet<string> invariantMethodNames;
        private readonly Config userConfig;
        private readonly GitRepoInfo gitRepoInfo;

        public static readonly JsonSerializerOptions JsonReaderOptions = new JsonSerializerOptions()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        public CombinedConfig(IEnumerable<string> additionalInvariantMethodNames, string projectPath, Config userConfig, GitRepoInfo gitRepoInfo)
        {
            // Someday this could deduce the project type from the assembly
            this.userConfig = userConfig;
            this.IsStrict = userConfig.IsStrict;
            this.ProjectPath = projectPath;

            this.TranslationCompiler = new SdvTranslationCompiler(this, projectPath);
            this.invariantMethodNames = this.GetInvariantMethodNames(additionalInvariantMethodNames);
            this.gitRepoInfo = gitRepoInfo;
        }

        public CombinedConfig(string projectPath, Config userConfig, GitRepoInfo gitRepoInfo)
            : this(Array.Empty<string>(), projectPath, userConfig, gitRepoInfo)
        { }

        public string ProjectPath { get; }

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
            var urlRoot = this.gitRepoInfo.GithubRepoRootUrl;
            if (urlRoot is null)
            {
                return null;
            }

            if (urlRoot.EndsWith(".git"))
            {
                urlRoot = urlRoot.Substring(0, urlRoot.Length - 4);
            }

            var repoRoot = this.gitRepoInfo.RepositoryPath;
            if (fullPath is null || urlRoot is null || repoRoot is null)
            {
                return null;
            }

            var relativePath = Path.GetRelativePath(repoRoot, fullPath);
            if (relativePath.StartsWith(".."))
            {
                return null;
            }

            if (this.gitRepoInfo.HeadCommit is null)
            {
                return null;
            }

            relativePath = relativePath.Replace('\\', '/');
            string fileUrl = $"{urlRoot}/blob/{this.gitRepoInfo.HeadCommit}/{HttpUtility.UrlEncode(relativePath).Replace("%2f", "/")}";
            if (line.HasValue)
            {
                fileUrl += $"#L{line.Value}";
            }

            return new Uri(fileUrl);
        }

        public string? GetHeadCommit() => this.gitRepoInfo.HeadCommit;

        private IReadOnlySet<string> GetInvariantMethodNames(IEnumerable<string> invariantMethodNamesFromAssembly)
        {
            HashSet<string> invariantMethods = new();

            // .net standard things that are common enough to warrant a special place in our heart.
            invariantMethods.UnionWith(this.TranslationCompiler.DomainSpecificInvariantMethodNames);

            invariantMethods.UnionWith(invariantMethodNamesFromAssembly);

            invariantMethods.UnionWith(this.userConfig.InvariantMethods);

            return invariantMethods;
        }

    }
}
