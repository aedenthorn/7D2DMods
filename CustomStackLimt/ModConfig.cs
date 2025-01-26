
using System.Collections.Generic;

namespace CustomStackLimit
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public float defaultMult = 1;
        public float customMult = 1;
        public float qualityMult = 1;
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