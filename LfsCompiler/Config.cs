using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocalizeFromSource
{
    public class Config
    {
        [JsonConstructor]
        public Config(bool isStrict, IReadOnlyCollection<Regex> invariantStringPatterns, IReadOnlyCollection<string> invariantMethods)
        {
            this.IsStrict = isStrict;
            this.InvariantStringPatterns = invariantStringPatterns.ToList();
            this.InvariantMethods = invariantMethods.ToList();
        }

        public Config()
            : this(false, Array.Empty<Regex>(), Array.Empty<string>())
        { }

        /// <summary>
        ///   If this is set, then the compiler will attempt to help you ensure that all of your
        ///   strings are marked with either a L() or I(), unless there's some hallmark that indicates
        ///   that the string is obviously an invariant one.  (E.g. because of how it is passed or the
        ///   shape of the string.
        /// </summary>
        public bool IsStrict { get; }

        /// <summary>
        ///   Patterns that indicate that the string is never going to be localized - e.g. if you have
        ///   some identifiers in your code and you always prefix your identifiers with the mod name,
        ///   that could save you the trouble of having to mark all your usages of them.
        /// </summary>
        public IReadOnlyCollection<Regex> InvariantStringPatterns { get; }

        /// <summary>
        ///   This is a list of full method names (e.g. namespace.class.method) that always accept
        ///   invariant strings.
        /// </summary>
        public IReadOnlyCollection<string> InvariantMethods { get; }
    }
}
