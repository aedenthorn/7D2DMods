﻿namespace VehicleRadio
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
 
        public string nextStationKey = "right";
        public string prevStationKey = "left";
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