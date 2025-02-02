namespace VehicleRadio
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
 
        public string spawnTraderKey = ".";
        public string nextStationKey = "left";
        public string prevStationKey = "right";
        public string toggleRadio = "end";
        public string volumeUpKey = "page up";
        public string volumeDownKey = "page down";
        public bool announceTrack = true;
        public bool defaultOn = true;
        public bool autoToggle = true;
        public int defaultStation = 1;
        public float spacialBlend = 0.7f;
        
        public string volumeText = "Radio Volume {0}%";
        public string toggleText = "Radio On: {0}";

    }
}