using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static LightingAround;
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
        //[HarmonyPatch(typeof(BlockValue), nameof(BlockValue.GetForceToOtherBlock))]
        static class BlockValue_GetForceToOtherBlock_Patch
        {

            static void Postfix(ref int __result)
            {
                if (!config.modEnabled || config.stabilityModifier <= 0)
                    return;
                __result = Mathf.RoundToInt(__result * config.stabilityModifier);
            }
        }
        [HarmonyPatch(typeof(StabilityCalculator), nameof(StabilityCalculator.CalcPhysicsStabilityToFall))]
        static class StabilityCalculator_CalcPhysicsStabilityToFall_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                if (!config.modEnabled)
                    return codes;
                Dbgl("Transpiling StabilityCalculator.CalcPhysicsStabilityToFall");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(BlockValue), nameof(BlockValue.GetForceToOtherBlock)))
                    {
                        Dbgl("Adding method to modify force to other block");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StabilityTweaks), nameof(StabilityTweaks.GetStabilityMod))));
                        i++;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        [HarmonyPatch(typeof(StabilityCalculator), nameof(StabilityCalculator.GetBlockStability), new Type[] { typeof(Vector3i), typeof(BlockValue) })]
        static class StabilityCalculator_GetBlockStability_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                if (!config.modEnabled)
                    return codes;
                Dbgl("Transpiling StabilityCalculator.GetBlockStability");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(BlockValue), nameof(BlockValue.GetForceToOtherBlock)))
                    {
                        Dbgl("Adding method to modify force to other block");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StabilityTweaks), nameof(StabilityTweaks.GetStabilityMod))));
                        i++;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static int GetStabilityMod(int value)
        {
            if (!config.modEnabled || config.stabilityModifier < 0)
                return value;
            return Mathf.RoundToInt(value * config.stabilityModifier);
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
