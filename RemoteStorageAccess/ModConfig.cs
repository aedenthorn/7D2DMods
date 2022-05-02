using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace RemoteStorageAccess
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public float pollInterval = 3;
        public float range = -1;

        public string openCurrentKey = "[";
        public string openNextKey = "right";
        public string openPrevKey = "left";
        public string openWindowKey = "]";

        public int windowPositionX = 40;
        public int windowPositionY = 40;
        public int buttonWidth = 100;
        public int buttonHeight = 30;
        public int betweenSpace = 10;
        public float windowHeight = Screen.height / 3;

        public byte windowBackgroundColorR = 255;
        public byte windowBackgroundColorG = 255;
        public byte windowBackgroundColorB = 255;
        public byte windowBackgroundColorA = 65;
        public byte currentColorR = 0;
        public byte currentColorG = 255;
        public byte currentColorB = 0;
        public byte currentColorA = 127;

        public string windowTitleText = "<b>Nearby Storage</b>";
        public int fontSize = 14;
        public string uiTitleText = "Nearby Storage";

    }
}