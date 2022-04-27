using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using WorldGenerationEngineFinal;

namespace FallDamage
{
    [BepInPlugin("aedenthorn.FallDamage", "Fall Damage", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        //public static ConfigEntry<int> nexusID;

        public static ConfigEntry<float> damageMult;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug.Value)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {

            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind<bool>("General", "IsDebug", true, "Enable debug logs");
            //nexusID = Config.Bind<int>("General", "NexusID", 88, "Nexus mod ID for updates");

            damageMult = Config.Bind<float>("Options", "DamageMult", 0f, "Fall damage multiplier.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
        }
        [HarmonyPatch(typeof(vp_PlayerDamageHandler), "OnMessage_FallImpact")]
        static class BlockObjectTool_Patch
        {
            static void Prefix(vp_PlayerDamageHandler __instance, ref float impact)
            {
                if (!modEnabled.Value)
                    return;

                __instance.DeathOnFallImpactThreshold = false;
                impact *= damageMult.Value;
            }
        }
    }
}
