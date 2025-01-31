using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace StabilityTweaks
{
    public class StabilityTweaks : IModApi
    {

        public static ModConfig config;
        public static StabilityTweaks context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }
        [HarmonyPatch(typeof(World), nameof(World.AddFallingBlock))]
        static class World_AddFallingBlocks_Patch
        {

            static bool Prefix()
            {
                if (!config.modEnabled || config.stabilityModifier > 0)
                    return true;
                return false;
            }
        }
        [HarmonyPatch(typeof(BlockValue), nameof(BlockValue.GetForceToOtherBlock))]
        static class BlockValue_GetForceToOtherBlock_Patch
        {

            static void Postfix(ref int __result)
            {
                if (!config.modEnabled || config.stabilityModifier <= 0)
                    return;
                __result = Mathf.RoundToInt(__result * config.stabilityModifier);
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
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
