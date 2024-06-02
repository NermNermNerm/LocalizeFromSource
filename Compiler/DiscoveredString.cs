using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalizeFromSource
{
    // Consider - the same string might appear in more than one location.  Perhaps we should record them all?

    public record DiscoveredString(string localizedString, bool isFormat, string? file, int? line);
}

