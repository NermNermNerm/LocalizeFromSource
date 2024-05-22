using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalizeFromSourceLib.Tests
{
    public class TestableSdvTranslationCompiler
        : SdvTranslationCompiler
    {
        public List<string> Errors { get; } = new List<string>();

        protected override void Error(int id, string message)
        {
            this.Errors.Add($"{id} - {message}");
        }
    }
}
