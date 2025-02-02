using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Path = System.IO.Path;

namespace DewCollectorTweaks
{
    public class DewCollectorTweaks : IModApi
    {

        public static ModConfig config;
        public static DewCollectorTweaks context;
        public static Mod mod;
        public static bool hidingItem;
        private static Transform holdingModel;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        [HarmonyPatch(typeof(BlockDewCollector), nameof(BlockDewCollector.Init))]
        static class BlockDewCollector_Init_Patch
        {

            static void Postfix(BlockDewCollector __instance)
            {
                if (!config.modEnabled)
                    return;
                __instance.ConvertToItem = config.convertToItem;
                __instance.ModdedConvertToItem = config.moddedConvertToItem;
            }
        }
        [HarmonyPatch(typeof(TileEntityDewCollector), nameof(TileEntityDewCollector.HandleLeftOverTime))]
        static class TileEntityDewCollector_HandleLeftOverTime_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling TileEntityDewCollector.HandleLeftOverTime");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(TileEntityDewCollector), nameof(TileEntityDewCollector.CurrentConvertCount)))
                    {
                        Dbgl($"Overriding CurrentConvertCount");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DewCollectorTweaks), nameof(DewCollectorTweaks.GetConvertCount))));
                        i++;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        [HarmonyPatch(typeof(TileEntityDewCollector), nameof(TileEntityDewCollector.HandleUpdate))]
        static class TileEntityDewCollector_HandleUpdate_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling TileEntityDewCollector.HandleUpdate");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(TileEntityDewCollector), nameof(TileEntityDewCollector.CurrentConvertCount)))
                    {
                        Dbgl($"Overriding CurrentConvertCount");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DewCollectorTweaks), nameof(DewCollectorTweaks.GetConvertCount))));
                        i++;
                    }
                    else if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(TileEntityDewCollector), nameof(TileEntityDewCollector.CurrentConvertSpeed)))
                    {
                        Dbgl($"Overriding CurrentConvertSpeed");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DewCollectorTweaks), nameof(DewCollectorTweaks.GetConvertSpeed))));
                        i++;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static int GetConvertCount(int result)
        {
            if(!config.modEnabled)
                return result;
            return Mathf.RoundToInt(result * config.collectMult);
        }

        private static float GetConvertSpeed(float result)
        {
            if(!config.modEnabled)
                return result;
            return result * config.speedMult;
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
