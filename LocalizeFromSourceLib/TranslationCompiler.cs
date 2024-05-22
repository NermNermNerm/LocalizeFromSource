using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalizeFromSourceLib
{
    public abstract class TranslationCompiler
    {
        /// <summary>
        ///   Error code when there are changes to the translations and the compiler is running in build lab mode
        ///   (where it cannot write.)
        /// </summary>
        public const int TranslationRequired = 1;

        public const int DefaultJsonUnusable = 2;

        public const int DefaultJsonInvalidUserEdit = 3;

        public const int LocaleJsonUnusable = 4;
        public const int LocaleEditsJsonUnusable = 5;

        public abstract void Compiled(string projectRoot, bool verifyOnly, DiscoveredString[] discoveredString);

        protected virtual void Error(int id,  string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"LFS{id:4}: {message}");
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
