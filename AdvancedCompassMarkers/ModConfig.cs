
using System.Collections.Generic;

namespace AdvancedCompassMarkers
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public string reloadKey = "end";
        public float defaultMinScale = 0.25f;
        public float defaultMaxScale = 1.5f;
        public float defaultMinDistance = 0f;
        public float defaultMaxDistance = 250;
        public Dictionary<string, MinMaxSettings> customMinMax = new Dictionary<string, MinMaxSettings>();
    }

    public class MinMaxSettings
    {
        public float minDistance = -1f;
        public float maxDistance = -1f;
        public float minScale = -1f;
        public float maxScale = -1f;
    }
}