using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace PickLockedDoors
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public string lockPickItem = "resourceLockPick";
        public float lockPickTime = 15;
        public float lockPickBreakChance = 0.75f;
    }
}