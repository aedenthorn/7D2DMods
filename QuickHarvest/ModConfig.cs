
using System.Collections.Generic;

namespace QuickHarvest
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public int blockRadius = 64;
        public bool replaceCrop = true;

        public string harvestKey = "h";
        public string harvestModKey = "";
        public List<string> harvestTypes = new List<string>();
    }
}