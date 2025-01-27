
using System.Collections.Generic;

namespace CustomStackLimit
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public float defaultMult = 5;
        public float customMult = 5;
        public Dictionary<string, string> namedMult = new Dictionary<string, string>()
        {
            { "itemNameHere", "500" },
            { "itemPrefixHere*", "2x" }
        };
        public Dictionary<string, string> numberMult = new Dictionary<string, string>()
        {
            { "500 (disabled)", "1000" },
            { "<50 (disabled)", "2x" }
        };
    }
}