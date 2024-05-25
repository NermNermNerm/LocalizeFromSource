using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalizeFromSource
{
    public record DiscoveredString(string localizedString, bool isFormat, string file, int line);
}

