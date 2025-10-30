using HarmonyLib;
using InControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public class TransferToForge : IModApi
    {

        public static ModConfig config;
        public static TransferToForge context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        [HarmonyPatch(typeof(XUiC_BackpackWindow), nameof(XUiC_BackpackWindow.GetBindingValueInternal))]
        static class XUiC_BackpackWindow_TryGetMoveDestinationInventory_Patch
        {
            static bool Prefix(XUiC_BackpackWindow __instance, ref string value, string bindingName, ref bool __result)
            {

                if (!config.modEnabled)
                    return true;
                if (bindingName == "lootingorvehiclestorage" && __instance.xui.playerUI.windowManager.IsWindowOpen("workstation_forge"))
                {
                    value = "true";
                    __result = true;
                    return false;
                }
                return true;
            }

        }

        [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
        static class XUiC_ItemStack_HandleMoveToPreferredLocation_Patch
        {

            static void Postfix(XUiC_ItemStack __instance)
            {
                if (!config.modEnabled || (__instance.StackLocation != XUiC_ItemStack.StackLocationTypes.Backpack && __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.ToolBelt) || !__instance.xui.playerUI.windowManager.IsWindowOpen("workstation_forge"))
                    return;
                XUiC_WorkstationMaterialInputWindow input = __instance.xui.GetChildByType<XUiC_WorkstationMaterialInputWindow>();
                TryMoveStack(__instance, input, true, true);
            }

        }

        [HarmonyPatch(typeof(XUiC_ContainerStandardControls), nameof(XUiC_ContainerStandardControls.MoveAll))]
        static class XUiC_ContainerStandardControls_MoveAll_Patch
        {
            static bool Prefix(XUiC_ContainerStandardControls __instance)
            {
                Dbgl(__instance.xui.playerUI.windowManager.HasWindow("workstation_forge"));

                if (!config.modEnabled || !__instance.xui.playerUI.windowManager.IsWindowOpen("workstation_forge"))
                    return true;

                var input = __instance.xui.GetChildByType<XUiC_WorkstationMaterialInputWindow>();
                if (input == null)
                    return true;
                var backpackWindow = __instance.xui.GetChildByType<XUiC_BackpackWindow>();
                if(backpackWindow == null)
                    return true;
                XUiC_ItemStack[] itemStackControllers = backpackWindow.backpackGrid.GetItemStackControllers();
                for (int i = 0; i < itemStackControllers.Length; i++)
                {
                    var stack = itemStackControllers[i];
                    TryMoveStack(stack, input, true, true);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(XUiC_ContainerStandardControls), nameof(XUiC_ContainerStandardControls.MoveFillAndSmart))]
        static class XUiC_ContainerStandardControls_MoveFillAndSmart_Patch
        {
            static bool Prefix(XUiC_ContainerStandardControls __instance)
            {
                if (!config.modEnabled || !__instance.xui.playerUI.windowManager.IsWindowOpen("workstation_forge"))
                    return true;
                var input = __instance.xui.GetChildByType<XUiC_WorkstationMaterialInputWindow>();
                if (input == null)
                    return true;
                var backpackWindow = __instance.xui.GetChildByType<XUiC_BackpackWindow>();
                if(backpackWindow == null)
                    return true;
                XUiC_ItemStack[] itemStackControllers = backpackWindow.backpackGrid.GetItemStackControllers();
                float unscaledTime = Time.unscaledTime;
                bool toEmpty = unscaledTime - XUiM_LootContainer.lastStashTime < 2f;
                for (int i = 0; i < itemStackControllers.Length; i++)
                {
                    var stack = itemStackControllers[i];
                    TryMoveStack(stack, input, true, toEmpty);
                }
                XUiM_LootContainer.lastStashTime = unscaledTime;
                return false;
            }

        }

        private static void TryMoveStack(XUiC_ItemStack stack, XUiC_WorkstationMaterialInputWindow input, bool combine, bool toEmpty)
        {
            if (stack.UserLockedSlot || stack.ItemStack.IsEmpty() || string.IsNullOrEmpty(stack.ItemStack?.itemValue?.ItemClass?.MadeOfMaterial?.ForgeCategory) || input.MaterialNames is null || !input.MaterialNames.Contains(stack.ItemStack.itemValue.ItemClass.MadeOfMaterial.ForgeCategory))
                return;

            Dbgl($"got forge item {stack.ItemStack.itemValue.ItemClass.Name}: {stack.ItemStack.itemValue.ItemClass.MadeOfMaterial.ForgeCategory}");
            var slots = input.inputGrid.GetSlots();
            if (combine)
            {
                for (int i = 0; i < input.inputGrid.workstationData.TileEntity.Input.Length && i < input.inputGrid.itemControllers.Length && i < slots.Length; i++)
                {
                    if (slots[i].CanStackWith(stack.ItemStack))
                    {
                        slots[i].count += stack.ItemStack.count;
                        stack.PlayPlaceSound(null);
                        stack.ItemStack = ItemStack.Empty.Clone();
                        stack.HandleSlotChangeEvent();
                        Dbgl($"added to slot {i}");
                        goto stored;
                    }
                    int num = stack.ItemStack.count;
                    if (stack.itemStack.itemValue.type == slots[i].itemValue.type && slots[i].CanStackPartly(ref num))
                    {
                        slots[i].count += num;
                        stack.ItemStack.count -= num;
                        stack.PlayPlaceSound(null);
                        stack.HandleSlotChangeEvent();
                        Dbgl($"added {num}/{stack.ItemStack.count} to slot {i}");
                        goto stored;
                    }
                }
            }
            if (toEmpty)
            {
                for (int i = 0; i < input.inputGrid.workstationData.TileEntity.Input.Length && i < input.inputGrid.itemControllers.Length && i < slots.Length; i++)
                {
                    if (slots[i].IsEmpty())
                    {
                        slots[i] = stack.ItemStack.Clone();
                        stack.PlayPlaceSound(null);
                        stack.ItemStack = ItemStack.Empty.Clone();
                        stack.HandleSlotChangeEvent();
                        Dbgl($"set to slot {i}");
                        goto stored;
                    }
                }
            }
            return;
        stored:
            input.inputGrid.SetSlots(slots);
            input.inputGrid.UpdateBackend(slots);
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
