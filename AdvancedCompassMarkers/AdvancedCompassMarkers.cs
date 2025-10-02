using HarmonyLib;
using InControl;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace AdvancedCompassMarkers
{
    public class AdvancedCompassMarkers : IModApi
    {

        public static ModConfig config;
        public static AdvancedCompassMarkers context;
        public static Mod mod;
        public static bool hidingItem;
        private static Transform holdingModel;
        private static int holdingModelIndex;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        [HarmonyPatch(typeof(NavObject), nameof(NavObject.GetCompassIconScale))]
        static class NavObject_GetCompassIconScale_Patch
        {

            static void Postfix(NavObject __instance, float _distance, ref float __result)
            {
                if (!config.modEnabled)
                    return;
                var minScale = config.defaultMin;
                var maxScale = config.defaultMax;
                var distance = Mathf.Clamp(_distance, config.minDistance, config.maxDistance);
                var closeness = config.maxDistance - distance; 
                float scale = minScale + closeness / (config.maxDistance - config.minDistance) * (maxScale - minScale);
                __result *= scale;
            }
        }

        public void LoadConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            if (!File.Exists(path))
            {
                config = new ModConfig();
            }
            else
            {
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
            }
            if(config.customMin == null)
            {
                config.customMin = new Dictionary<string, float>();
                foreach(var e in Enum.GetNames(typeof(EnumMapObjectType)))
                {
                    config.customMin[e] = 0;
                }
            }
            if(config.customMax == null)
            {
                config.customMax = new Dictionary<string, float>();
                foreach(var e in Enum.GetNames(typeof(EnumMapObjectType)))
                {
                    config.customMax[e] = 0;
                }
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
