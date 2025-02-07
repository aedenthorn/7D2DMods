
using System.Collections.Generic;

namespace PreserveFallenBlocks
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public List<string> allowedTypes = new List<string>();
        public List<string> ignoreTypes = new List<string>();
    }
}