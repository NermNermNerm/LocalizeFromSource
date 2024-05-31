using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalizeFromSourceLib.Tests
{
    public class TestableSdvTranslator
        : SdvTranslator
    {
        private readonly string i18nFolder;

        public TestableSdvTranslator(string i18nFolder, Func<string> localeGetter, string sourceLocale = "en-us")
            : base(localeGetter, sourceLocale)
        {
            this.i18nFolder = i18nFolder;
        }

        protected override string? GetI18nFolder()
        {
            return this.i18nFolder;
        }
    }
}
