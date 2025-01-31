using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace ToggleHoldingItem
{
    public class ToggleHoldingItem : IModApi
    {

        public static ModConfig config;
        public static ToggleHoldingItem context;
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

        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null || ___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsModalWindowOpen())
                    return;
                if (AedenthornUtils.CheckKeyDown(config.toggleKey) && AedenthornUtils.CheckKeyHeld(config.toggleModKey, false))
                {
                    hidingItem = !hidingItem;
                    Dbgl($"Pressed toggle key; hiding item {hidingItem}");
                    if (hidingItem)
                    {
                        holdingModel = GameManager.Instance.World.GetPrimaryPlayer().inventory.models[GameManager.Instance.World.GetPrimaryPlayer().inventory.m_HoldingItemIdx];
                        GameManager.Instance.World.GetPrimaryPlayer().inventory.models[GameManager.Instance.World.GetPrimaryPlayer().inventory.m_HoldingItemIdx] = null;
                    }
                    else
                    {
                        GameManager.Instance.World.GetPrimaryPlayer().inventory.models[GameManager.Instance.World.GetPrimaryPlayer().inventory.m_HoldingItemIdx] = holdingModel;
                    }
                    GameManager.Instance.World.GetPrimaryPlayer().inventory.ForceHoldingItemUpdate();
                }
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemItemValue))]
        [HarmonyPatch(MethodType.Getter)]
        static class Inventory_holdingItemItemValue_Patch
        {

            static bool Prefix(Inventory __instance, ref ItemValue __result)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return true;
                __result = __instance.bareHandItemValue;
                return false;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItem))]
        [HarmonyPatch(MethodType.Getter)]
        static class Inventory_holdingItem_Patch
        {

            static bool Prefix(Inventory __instance, ref ItemClass __result)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return true;
                __result = __instance.bareHandItem;
                return false;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemData))]
        [HarmonyPatch(MethodType.Getter)]
        static class Inventory_holdingItemData_Patch
        {

            static bool Prefix(Inventory __instance, ref ItemInventoryData __result)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return true;
                __result = __instance.bareHandItemInventoryData;
                return false;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
        static class Inventory_setHoldingItemTransform_Patch
        {

            static void Prefix(Inventory __instance, ref Transform _t)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return;
                _t = null;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HoldingItemHasChanged))]
        static class Inventory_HoldingItemHasChanged_Patch
        {

            static void Prefix(Inventory __instance)
            {
                if(__instance.entity is EntityPlayerLocal)
                {
                    if(holdingModel != null)
                    {
                        GameObject.Destroy(holdingModel.gameObject);
                    }
                    hidingItem = false;
                }
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
