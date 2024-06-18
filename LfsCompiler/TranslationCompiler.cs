using System.Text.RegularExpressions;

namespace LocalizeFromSource
{
    public abstract class TranslationCompiler
    {
        protected bool anyErrorsReported = false;

        public const string ErrorPrefix = "LFS";

        private readonly Regex hasAnyLettersInIt = new Regex(@"\p{L}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        ///   Error code when there are changes to the translations and the compiler is running in build lab mode
        ///   (where it cannot write.)
        /// </summary>
        public const int TranslationRequired = 1;
        public const int DefaultJsonUnusable = 2;
        public const int LocaleJsonUnusable = 4;
        public const int LocaleEditsJsonUnusable = 5;
        public const int StringNotMarked = 6;
        public const int ImproperUseOfMethod = 7;
        public const int LocalizingEmpty = 8;
        public const int BadConfigFile = 9;
        public const int MissingIngestionFiles = 10;
        public const int IncompleteTranslation = 11;
        public const int IncompatibleSource = 12;
        public const int MungedTranslationFile = 13;
        public const int BadFile = 14;
        public const int IngestingOutOfSync = 15;

        public abstract bool GenerateI18nFiles(IReadOnlyCollection<DiscoveredString> discoveredString);

        protected virtual void Error(int id, string message)
        {
            // https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks?view=vs-2022
            Console.WriteLine($"LocalizeFromSource : error {ErrorPrefix}{id:4} : {message}");
            anyErrorsReported = true;
        }

        public virtual bool IsKnownInvariantString(string s)
        {
            return !hasAnyLettersInIt.IsMatch(s);
        }

        public virtual IEnumerable<string> DomainSpecificInvariantMethodNames { get; } = ["System.Text.RegularExpressions.Regex..ctor"];
    }
}
