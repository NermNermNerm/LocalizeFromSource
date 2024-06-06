using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalizeFromSource;

namespace LocalizeFromSourceTests
{
    public class TestableSdvTranslationCompiler
        : SdvTranslationCompiler
    {
        public TestableSdvTranslationCompiler(CombinedConfig config, string projectPath) : base(config, projectPath) { }

        public List<string> Errors { get; } = new List<string>();

        protected override void Error(int id, string message)
        {
            this.Errors.Add($"{id} - {message}");
        }
    }
}
