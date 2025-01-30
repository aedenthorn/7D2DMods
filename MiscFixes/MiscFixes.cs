using HarmonyLib;
using Newtonsoft.Json;
using SteelSeries.GameSense;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UAI;
using UnityEngine;
using static ItemActionReplaceBlock;
using static ReflectionManager;
using Path = System.IO.Path;

namespace MiscFixes
{
    public class MiscFixes : IModApi
    {

        public static ModConfig config;
        public static MiscFixes context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

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

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                UnityEngine.Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

        [HarmonyPatch(typeof(AvatarZombieController), nameof(AvatarZombieController.GetMainZombieBodyMaterial))]
        static class AvatarZombieController_GetMainZombieBodyMaterial_Patch
        {

            static bool Prefix(AvatarZombieController __instance, ref Material __result)
            {
                if (!config.modEnabled)
                    return true;
                if (__instance.entity?.emodel?.meshTransform?.GetComponent<Renderer>() == null)
                {
                    __result = null;
                    return false;
                }
                return true;
            }
        }
    }
}
