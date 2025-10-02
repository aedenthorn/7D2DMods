using HarmonyLib;
using InControl;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace AdvancedCompassMarkers
{
    public class ToggleHoldingItem : IModApi
    {

        public static ModConfig config;
        public static ToggleHoldingItem context;
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

        [HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
        static class PlayerMoveController_Update_Patch
        {

            static void Prefix(PlayerMoveController __instance)
            {
                if (!config.modEnabled || __instance.entityPlayerLocal.AttachedToEntity != null || GameManager.Instance.isAnyCursorWindowOpen(null))
                    return;
                if (AedenthornUtils.CheckKeyDown(config.toggleKey))
                {
                    ToggleItem();
                    return;
                }
                int newSlot = __instance.playerInput.InventorySlotWasPressed;
                if (newSlot >= 0)
                {
                    if (__instance.playerInput.LastInputType == BindingSourceType.DeviceBindingSource)
                    {
                        if (__instance.entityPlayerLocal.AimingGun)
                        {
                            newSlot = -1;
                        }
                    }
                    else if (InputUtils.ShiftKeyPressed && __instance.entityPlayerLocal.inventory.PUBLIC_SLOTS > __instance.entityPlayerLocal.inventory.SHIFT_KEY_SLOT_OFFSET)
                    {
                        newSlot += __instance.entityPlayerLocal.inventory.SHIFT_KEY_SLOT_OFFSET;
                    }
                }
                if (__instance.inventoryScrollPressed && __instance.inventoryScrollIdxToSelect != -1)
                {
                    newSlot = __instance.inventoryScrollIdxToSelect;
                }
                if(newSlot > -1 && newSlot == __instance.entityPlayerLocal.inventory.GetFocusedItemIdx())
                {
                    ToggleItem();

                }

            }

            private static void ToggleItem()
            {
                hidingItem = !hidingItem;
                int idx = GameManager.Instance.World.GetPrimaryPlayer().inventory.m_HoldingItemIdx;
                if (hidingItem)
                {
                    holdingModelIndex = idx;
                    holdingModel = GameManager.Instance.World.GetPrimaryPlayer().inventory.models[idx];
                    GameManager.Instance.World.GetPrimaryPlayer().inventory.models[idx] = null;
                }
                else
                {
                    GameManager.Instance.World.GetPrimaryPlayer().inventory.models[idx] = holdingModel;
                }
                GameManager.Instance.World.GetPrimaryPlayer().inventory.updateHoldingItem();
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemItemValue))]
        [HarmonyPatch(MethodType.Getter)]
        public static class Inventory_holdingItemItemValue_Patch
        {

            public static bool Prefix(Inventory __instance, ref ItemValue __result)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return true;
                __result = __instance.bareHandItemValue;
                return false;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItem))]
        [HarmonyPatch(MethodType.Getter)]
        public static class Inventory_holdingItem_Patch
        {

            public static bool Prefix(Inventory __instance, ref ItemClass __result)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return true;
                __result = __instance.bareHandItem;
                for (int i = 0; i < __result.Actions.Length; i++)
                {
                    if (__result.Actions[i] is ItemActionDynamicMelee)
                    {
                        (__result.Actions[i] as ItemActionDynamicMelee).RangeDefault = 2;
                    }
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.holdingItemData))]
        [HarmonyPatch(MethodType.Getter)]
        public static class Inventory_holdingItemData_Patch
        {

            public static bool Prefix(Inventory __instance, ref ItemInventoryData __result)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return true;
                __result = __instance.bareHandItemInventoryData;
                for (int i = 0; i < __result.item.Actions.Length; i++)
                {
                    if (__result.item.Actions[i] is ItemActionDynamicMelee)
                    {
                        (__result.item.Actions[i] as ItemActionDynamicMelee).RangeDefault = 2;
                    }
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.setHoldingItemTransform))]
        public static class Inventory_setHoldingItemTransform_Patch
        {

            public static void Prefix(Inventory __instance, ref Transform _t)
            {
                if (!config.modEnabled || !hidingItem || !(__instance.entity is EntityPlayerLocal))
                    return;
                _t = null;
            }
        }
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HoldingItemHasChanged))]
        public static class Inventory_HoldingItemHasChanged_Patch
        {

            public static void Prefix(Inventory __instance)
            {
                if(__instance.entity is EntityPlayerLocal && hidingItem)
                {
                    GameManager.Instance.World.GetPrimaryPlayer().inventory.models[holdingModelIndex] = holdingModel;
                    holdingModel = null;
                    hidingItem = false;
                }
            }
        }
        [HarmonyPatch(typeof(ItemActionEat), nameof(ItemActionEat.ExecuteAction))]
        public static class ItemActionEat_ExecuteAction_Patch
        {

            public static void Prefix(ItemActionEat __instance, ItemActionData _actionData)
            {
                if(_actionData.invData.holdingEntity is EntityPlayerLocal && hidingItem)
                {
                    __instance.Consume = false;
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
