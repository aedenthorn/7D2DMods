
using System.Collections.Generic;

namespace AdvancedCompassMarkers
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public float defaultMin = 0.25f;
        public float defaultMax = 1.5f;
        public float maxDistance = 500;
        public float minDistance = 0f;
        public Dictionary<string, float> customMin;
        public Dictionary<string, float> customMax;
    }
}