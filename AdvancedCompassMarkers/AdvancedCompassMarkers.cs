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
                NavObjectCompassSettings currentCompassSettings = __instance.CurrentCompassSettings;
                string text = __instance.GetSpriteName(currentCompassSettings);
                bool changed = false;
                if(!config.customMinMax.TryGetValue(text, out var settings))
                {
                    config.customMinMax[text] = null;
                    SaveConfig();
                }
                if (settings == null)
                {
                    settings = new MinMaxSettings();
                }
                if (settings.minDistance < 0)
                    settings.minDistance = config.defaultMinDistance;
                if (settings.maxDistance < 0)
                    settings.maxDistance = config.defaultMaxDistance;
                if (settings.minScale < 0)
                    settings.minScale = config.defaultMinScale;
                if (settings.maxScale < 0)
                    settings.maxScale = config.defaultMaxScale;

                var distance = Mathf.Clamp(_distance, settings.minDistance, settings.maxDistance);
                var closeness = settings.maxDistance - distance; 
                float scale = settings.minScale + closeness / (settings.maxDistance - settings.minDistance) * (settings.maxScale - settings.minScale);
                __result *= scale;
            }
        }

        public static void LoadConfig()
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
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void SaveConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
