using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace CraftFromContainers
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = false;
        public bool enableForRepairAndUpgrade = true;
        public bool enableForTrader = true;
        public bool enableForRefuel = true;
        public bool enableForReload = true;
        public bool enableFromVehicles = true;
        public bool allowLockedContainers = true;
        public bool allowAllContainers = true;
        public float range = -1;
    }
}