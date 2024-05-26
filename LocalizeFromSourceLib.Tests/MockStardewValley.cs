using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Note that this application only looks for matching names, not matching assemblies, so
//  we can create this to help verify it ignores calls to the domain-specific invariants.

namespace StardewValley
{
    public class GameLocation
    {
        public void playSound(string s) { }
    }
}
