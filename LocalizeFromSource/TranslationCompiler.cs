using LocalizeFromSourceLib;
using Mono.Cecil;

namespace LocalizeFromSource
{
    public abstract class TranslationCompiler
    {
        protected bool anyErrorsReported = false;

        public const string ErrorPrefix = "LFS";

        /// <summary>
        ///   Error code when there are changes to the translations and the compiler is running in build lab mode
        ///   (where it cannot write.)
        /// </summary>
        public const int TranslationRequired = 1;
        public const int DefaultJsonUnusable = 2;
        public const int DefaultJsonInvalidUserEdit = 3;
        public const int LocaleJsonUnusable = 4;
        public const int LocaleEditsJsonUnusable = 5;
        public const int StringNotMarked = 6;
        public const int ImproperUseOfMethod = 7;
        public const int LocalizingEmpty = 8;

        public abstract bool GenerateI18nFiles(string projectRoot, bool verifyOnly, IReadOnlyCollection<DiscoveredString> discoveredString);

        public IReadOnlySet<string> GetInvariantMethodNames(AssemblyDefinition dll)
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

            invariantMethods.UnionWith(this.DomainSpecificInvariantMethodNames);

            invariantMethods.UnionWith(this.GetMethodsWithCustomAttribute(dll));

            return invariantMethods;
        }

        private IEnumerable<string> GetMethodsWithCustomAttribute(AssemblyDefinition assembly)
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

        protected abstract IEnumerable<string> DomainSpecificInvariantMethodNames { get; }

        protected virtual void Error(int id,  string message)
        {
            // https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks?view=vs-2022
            Console.WriteLine($"LocalizeFromSource : error {ErrorPrefix}{id:4} : {message}");
            anyErrorsReported = true;
        }

        /*
         * 
         * Starting condition: Empty folder
         * Run compiler, produces 'default.json'
         * German translator produces 'de.json'
         * Developer changes a string and adds a new one and runs compiler again
         *   Compiler has the old default.json and the new one, plus the de.json.
         *   It can see use fuzzy-matching to match the old key to the new one, and so
         *   can generate a new de.json that may use the stale translation.  It would have
         *   to produce a new file, call it 'out-of-date.de.json' that lists the key-value-pair
         *   of the new string that's missing, say:
         *   { out-of-date: [ { "key": "1afe", "old-en": null, "new-en": "I'm a little teapot", "old-de": null, "new-de": null } ] }
         * The German translator can then refer to that one file and know all the translations
         *   that need to be touched up - they'd update 'new-de' with new values and run
         *   `UpdateTranslations` to move all the 'new-de' values to 'de.json'.
         * Alternatively, the German translator can just do it the old way - 
         * 
         */
    }
}
