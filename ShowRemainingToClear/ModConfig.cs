using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace ShowRemainingToClear
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public bool showAllSleepers = true;
        public string remainingText = "{0} ({1} left)";
        public string showAllPointsKey = "end";
    }
}