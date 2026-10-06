using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace TransferToForge
{
    public class TransferMods : IModApi
    {

        public static ModConfig config;
        public static TransferMods context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }
        public static void SwapMods(ItemStack stack1, ItemStack stack2)
        {

            var oldCount = stack1.itemValue.Modifications.Where(m => m != null && !m.IsEmpty()).Count();
            var newCount = stack2.itemValue.Modifications.Where(m => m != null && !m.IsEmpty()).Count();
            Dbgl($"{oldCount} old mods, {newCount} new mods");
            if (oldCount <= stack2.itemValue.Modifications.Length && newCount <= stack1.itemValue.Modifications.Length)
            {
                var oldMods = new List<ItemValue>();
                var newMods = new List<ItemValue>();
                foreach (var m in stack2.itemValue.Modifications)
                {
                    if (m != null && !m.IsEmpty())
                    {
                        oldMods.Add(m);
                    }
                }
                foreach (var m in stack1.itemValue.Modifications)
                {
                    if (m != null && !m.IsEmpty())
                    {
                        newMods.Add(m);
                    }
                }
                stack1.itemValue.Modifications = oldMods.ToArray();
                stack2.itemValue.Modifications = newMods.ToArray();
                Dbgl($"Switched {oldCount} mods from old equipment with {newCount} mods from new equipment");
            }
        }

        [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.SwapItem))]
        static class XUiC_ItemStack_SwapItem_Patch
        {
            static void Prefix(XUiC_ItemStack __instance)
            {
                Dbgl($"key down {AedenthornUtils.CheckKeyHeld(config.modKey)}");
                
                if (!config.modEnabled || __instance.xui?.dragAndDrop?.CurrentStack?.itemValue?.ItemClass?.IsEquipment != true || __instance.itemStack?.itemValue?.ItemClass?.IsEquipment != true || !AedenthornUtils.CheckKeyHeld(config.modKey))
                    return;

                Dbgl($"Swapping {__instance.ItemStack?.itemValue?.ItemClass?.GetItemName()} with {__instance.xui.dragAndDrop.CurrentStack?.itemValue?.ItemClass?.GetItemName()} ");

                SwapMods(__instance.ItemStack, __instance.xui.dragAndDrop.CurrentStack);
            }

        }

        [HarmonyPatch(typeof(XUiM_PlayerEquipment), nameof(XUiM_PlayerEquipment.EquipItem))]
        static class XUiM_PlayerEquipment_EquipItem_Patch
        {
            static void Postfix(XUiM_PlayerEquipment __instance, ItemStack _stack, ref ItemStack __result)
            {
                Dbgl($"equipped {_stack?.itemValue?.ItemClass?.GetItemName()}, replacing {__result?.itemValue?.ItemClass?.GetItemName()}");
                
                Dbgl($"key down {AedenthornUtils.CheckKeyHeld(config.modKey)}");

                if (!config.modEnabled || __result?.IsEmpty() != false || _stack?.IsEmpty() != false || _stack == __result || !AedenthornUtils.CheckKeyHeld(config.modKey))
                    return;
                SwapMods(__result, _stack);
            }
        }
        public static void LoadConfig()
        {
            Dbgl($"mod path: {mod.Path}");
            var path = Path.Combine(mod.Path, "config.json");
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
