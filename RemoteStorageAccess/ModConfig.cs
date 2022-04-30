using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace RemoteStorageAccess
{
    public class ModConfig
    {
        public ModConfig()
        {
            modEnabled = true;
            isDebug = true;
            pollInterval = 3;

            openCurrentKey = "[";
            openNextKey = "right";
            openPrevKey = "left";
            openWindowKey = "]";

            windowPositionX = 40;
            windowPositionY = 40;
            buttonWidth = 100;
            buttonHeight = 30;
            betweenSpace = 10;
            windowHeight = Screen.height / 3;
            windowBackgroundColorR = 255;
            windowBackgroundColorG = 255;
            windowBackgroundColorB = 255;
            windowBackgroundColorA = 65;
            currentColorR = 0;
            currentColorG = 255;
            currentColorB = 0;
            currentColorA = 127;

            windowTitleText = "<b>Nearby Storage</b>";
            fontSize = 14;
            uiTitleText = "Nearby Storage";
        }

        public bool modEnabled;
        public bool isDebug;
        public int windowPositionX;
        public int windowPositionY;
        public int fontSize;
        public int buttonWidth;
        public int buttonHeight;
        public int betweenSpace;
        public float windowHeight;
        public float pollInterval;
        public byte windowBackgroundColorR;
        public byte windowBackgroundColorG;
        public byte windowBackgroundColorB;
        public byte windowBackgroundColorA;

        public byte currentColorR;
        public byte currentColorG;
        public byte currentColorB;
        public byte currentColorA;

        public string windowTitleText;
        public string uiTitleText;

        public string openCurrentKey;
        public string openNextKey;
        public string openPrevKey;
        public string openWindowKey;
    }
}