
using UnityEngine;

namespace UnrestrictedTraderAccess
{
    public class ModConfig
    {
        public bool modEnabled = true;
        public bool isDebug = true;
        public bool removePlaceProtection = true;
        public bool removeLandClaimProtection = true;
        public bool removeDamageProtection = false;
        public string damageProtectionDisabledText = "Trader damage protection disabled";
        public string damageProtectionEnabledText = "Trader damage protection enabled";
        public string[] alwaysAllowDamageTypes = { "blockNameHere", "blockPrefixHere*" };
        public string toggleKey = "[0]";

    }
}