using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using UnityEngine;
using Path = System.IO.Path;

namespace CustomStackLimit
{
    public class CustomStackLimit : IModApi
    {

        public static ModConfig config;
        public static CustomStackLimit context;
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
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }
        [HarmonyPatch(typeof(ItemClass), new Type[] { })]
        [HarmonyPatch(MethodType.Constructor)]
        static class ItemClass_Patch
        {
            static void Postfix(ItemClass __instance)
            {
                if (!config.modEnabled)
                    return;
                __instance.Stacknumber = new DataItem<int>(Mathf.RoundToInt(__instance.Stacknumber.Value * config.defaultMult)); 
            }
        }

        [HarmonyPatch(typeof(ItemClassBlock), new Type[] { })]
        [HarmonyPatch(MethodType.Constructor)]
        static class ItemClassBlock_Patch
        {
            static void Postfix(ItemClassBlock __instance)
            {
                if (!config.modEnabled)
                    return;
                __instance.Stacknumber = new DataItem<int>(Mathf.RoundToInt(__instance.Stacknumber.Value * config.defaultMult)); 
            }
        }
        [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanStackWith))]
        static class ItemStack_CanStackWith_Patch
        {
            static void Postfix(ItemStack __instance, ItemStack _other, ref bool __result)
            {
                if (!config.modEnabled || !__result)
                    return;
                if (__instance.itemValue != null && _other.itemValue != null && __instance.itemValue.HasQuality && _other.itemValue.HasQuality)
                {
                    if(__instance.itemValue.Quality != _other.itemValue.Quality)
                        __result = false;
                    else if (__instance.itemValue.Modifications.Length > 0 || _other.itemValue.Modifications.Length > 0)
                        __result = false;
                    else if (__instance.itemValue.UseTimes != _other.itemValue.UseTimes)
                        __result = false;
                } 
            }
        }
        //[HarmonyPatch(typeof(ItemClass), nameof(ItemClass.LateInit))]
        static class ItemClass_LateInit_Patch
        {
            static void Postfix(ItemClass __instance)
            {
                if (!config.modEnabled || !__instance.HasQuality)
                    return;
                //__instance.Stacknumber = new DataItem<int>(Mathf.RoundToInt(__instance.Stacknumber.Value * config.qualityMult)); 
            }
        }
        [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.CreateItemsFromBlocks))]
        static class ItemClassesFromXml_CreateItemsFromBlocks_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling method ItemClassesFromXml.CreateItemsFromBlocks");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Stfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Stacknumber)))
                    {
                        if (codes[i - 2].opcode != OpCodes.Ldc_I4)
                        {
                            Dbgl($"\tAdding method to modify custom stacks");
                            codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CustomStackLimit), nameof(CustomStackLimit.OverrideCustomValueForBlocks))));
                            codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_0));
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }
        [HarmonyPatch(typeof(ItemClassesFromXml), nameof(ItemClassesFromXml.parseItem))]
        static class ItemClassesFromXml_parseItem_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling method ItemClassesFromXml.parseItem");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Stfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Stacknumber)))
                    {
                        if (codes[i - 2].opcode != OpCodes.Ldc_I4)
                        {
                            Dbgl($"\tAdding method to modify custom stacks");
                            codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CustomStackLimit), nameof(CustomStackLimit.OverrideCustomValue))));
                            codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                            break;
                        }
                        else
                        {
                            Dbgl($"\tAdding method to modify default stacks");
                            codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CustomStackLimit), nameof(CustomStackLimit.OverrideDefaultValue))));
                            codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }
        [HarmonyPatch(typeof(ItemModificationsFromXml), nameof(ItemModificationsFromXml.parseItem))]
        static class ItemModificationsFromXml_parseItem_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling method ItemModificationsFromXml.parseItem");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Stfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(ItemClass), nameof(ItemClass.Stacknumber)))
                    {
                        if (codes[i - 2].opcode != OpCodes.Ldc_I4)
                        {
                            Dbgl($"\tAdding method to modify custom stacks");
                            codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CustomStackLimit), nameof(CustomStackLimit.OverrideCustomValue))));
                            codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                            break;
                        }
                        else
                        {
                            Dbgl($"\tAdding method to modify default stacks");
                            codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CustomStackLimit), nameof(CustomStackLimit.OverrideDefaultValue))));
                            codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0));
                            break;
                        }
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static DataItem<int> OverrideDefaultValue(DataItem<int> defaultData, XElement elementItem)
        {
            if (!config.modEnabled)
                return defaultData;
            var name = elementItem.GetAttribute("name");
            return GetDataItem(defaultData, name, config.defaultMult);
        }
        private static DataItem<int> OverrideCustomValue(DataItem<int> defaultData, XElement elementItem)
        {
            if (!config.modEnabled)
                return defaultData;
            var name = elementItem.GetAttribute("name");
            return GetDataItem(defaultData, name, config.customMult);
        }

        private static DataItem<int> OverrideCustomValueForBlocks(DataItem<int> defaultData, int idx)
        {
            if (!config.modEnabled)
                return defaultData;
            var name = Block.list[idx].GetBlockName();
            return GetDataItem(defaultData, name, config.customMult);
        }

        private static DataItem<int> GetDataItem(DataItem<int> defaultData, string name, float mult)
        {
            foreach (var kvp in config.namedMult)
            {
                if (kvp.Key == name || (kvp.Key.EndsWith("*") && name.StartsWith(kvp.Key.Substring(0, kvp.Key.Length - 1))))
                {
                    return GetDataItem(defaultData, kvp.Value);
                }
            }
            foreach (var kvp in config.numberMult)
            {
                int result;
                if (int.TryParse(kvp.Key, out result))
                {
                    if (result == defaultData.Value)
                    {
                        return GetDataItem(defaultData, kvp.Value);
                    }
                }
                if (kvp.Key.StartsWith(">") && int.TryParse(kvp.Key.Substring(1), out result) && defaultData.Value > result)
                {
                    return GetDataItem(defaultData, kvp.Value);
                }
                if (kvp.Key.StartsWith("<") && int.TryParse(kvp.Key.Substring(1), out result) && defaultData.Value < result)
                {
                    return GetDataItem(defaultData, kvp.Value);
                }
            }
            return new DataItem<int>(Mathf.RoundToInt(defaultData.Value * mult));
        }
        private static DataItem<int> GetDataItem(DataItem<int> orig, string multOrAmount)
        {
            int i;
            if (int.TryParse(multOrAmount, out i))
            {
                return new DataItem<int>(i);
            }
            float f;
            if (multOrAmount.EndsWith("x") && float.TryParse(multOrAmount.Substring(0, multOrAmount.Length - 1), out f))
            {
                return new DataItem<int>(Mathf.RoundToInt(orig.Value * f));
            }
            return orig;
        }
    }
}
