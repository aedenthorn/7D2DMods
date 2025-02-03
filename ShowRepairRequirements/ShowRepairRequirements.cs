using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ShowRepairRequirements
{
    public class ShowRepairRequirements : IModApi
    {

        public static ModConfig config;
        public static ShowRepairRequirements context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        public static string repairTooltip;
        [HarmonyPatch(typeof(XUiC_ItemActionEntry), nameof(XUiC_ItemActionEntry.OnHover))]
        static class XUiC_ItemActionEntry_OnHover_Patch
        {
            static void Postfix(XUiC_ItemActionEntry __instance, XUiController _sender, bool _isOver)
            {
                repairTooltip = "";
                if (!config.modEnabled || !(__instance.itemActionEntry is ItemActionEntryRepair) || (__instance.itemActionEntry as ItemActionEntryRepair).state == ItemActionEntryRepair.StateTypes.RecipeLocked)
                    return;
                if (__instance.xui.currentToolTip != null)
                {
                    if (_isOver)
                    {
                        string tooltip = "";
                        ItemClass forId = ItemClass.GetForId(((XUiC_ItemStack)__instance.ItemActionEntry.ItemController).ItemStack.itemValue.type);
                        ItemValue itemValue = ((XUiC_ItemStack)__instance.ItemActionEntry.ItemController).ItemStack.itemValue;

                        if (forId.RepairTools != null && forId.RepairTools.Length > 0)
                        {
                            ItemClass itemClass = ItemClass.GetItemClass(forId.RepairTools[0].Value, false);
                            if (itemClass != null)
                            {
                                int num = Convert.ToInt32(Math.Ceiling((double)(Mathf.CeilToInt(itemValue.UseTimes) / (float)itemClass.RepairAmount.Value)));
                                int has = __instance.xui.PlayerInventory.GetItemCount(new ItemValue(itemClass.Id, false));
                                tooltip += $"{Localization.Get(itemClass.Name, false)} {has}/{num}\n";
                            }
                        }
                        repairTooltip = tooltip;
                    }
                    else
                    {
                        repairTooltip = "";
                    }
                }
            }
        }
        [HarmonyPatch(typeof(XUiView), nameof(XUiView.OnHover))]
        static class XUiView_OnHover_Patch
        {
            static void Postfix(XUiView __instance, bool _isOver)
            {
                if (!config.modEnabled || string.IsNullOrEmpty(repairTooltip) || __instance.xui.currentToolTip == null)
                    return;
                //Dbgl($"{__instance.GetType()} {__instance.ID}");
                __instance.xui.currentToolTip.ToolTip = repairTooltip;
            }
        }
        [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.OnHUD))]
        static class ItemActionAttack_OnHUD_Patch
        {
            static void Postfix(ItemActionAttack __instance, ItemActionData _actionData )
            {
                if (!config.modEnabled || !config.showForBlocks || !(__instance is ItemActionRepair))
                    return;
                ItemActionRepair iar = __instance as ItemActionRepair;
                ItemActionAttackData itemActionAttackData = _actionData as ItemActionAttackData;
                if (!__instance.canShowOverlay(itemActionAttackData) || !__instance.isShowOverlay(itemActionAttackData))
                    return;
                ItemInventoryData invData = _actionData.invData;
                EntityPlayerLocal entityPlayerLocal = itemActionAttackData.invData.holdingEntity as EntityPlayerLocal;
                if (!entityPlayerLocal)
                {
                    return;
                }
                LocalPlayerUI _playerUi = LocalPlayerUI.GetUIForPlayer(entityPlayerLocal);
                if (_playerUi == null || _playerUi.xui == null)
                {
                    return;
                }
                XUiController xuiController = _playerUi.xui.FindWindowGroupByName(XUiC_FocusedBlockHealth.ID);
                XUiC_FocusedBlockHealth xuiC_FocusedBlockHealth = xuiController?.GetChildByType<XUiC_FocusedBlockHealth>();
                if (xuiC_FocusedBlockHealth == null)
                {
                    return;
                }
                BlockValue blockValue = _actionData.invData.hitInfo.hit.blockValue;
                if (blockValue.ischild)
                {
                    Vector3i parentPos = blockValue.Block.multiBlockPos.GetParentPos(_actionData.invData.hitInfo.hit.blockPos, blockValue);
                    blockValue = _actionData.invData.world.GetBlock(parentPos);
                }
                Block block = blockValue.Block;
                if (!block.CanRepair(blockValue))
                {
                    return;
                }
                int repairAmount = Utils.FastMin((int)iar.repairAmount, blockValue.damage);
                float repairFraction = repairAmount / (float)block.MaxDamage;
                List<Block.SItemNameCount> list = block.RepairItems;
                if (block.RepairItemsMeshDamage != null && block.shape.UseRepairDamageState(blockValue))
                {
                    repairAmount = 1;
                    repairFraction = 1f;
                    list = block.RepairItemsMeshDamage;
                }
                if (list == null)
                {
                    return;
                }
                float resourceScale = block.ResourceScale;

                for (int i = 0; i < list.Count; i++)
                {
                    string itemName = list[i].ItemName;
                    float reqFloat = list[i].Count * repairFraction * resourceScale;
                    int reqInt = Utils.FastMax((int)reqFloat, 1);
                    ItemStack itemStack = new ItemStack(ItemClass.GetItem(itemName, false), reqInt);
                    int hasCount = invData.holdingEntity.inventory.GetItemCount(itemStack.itemValue, false, -1, -1, true) + invData.holdingEntity.bag.GetItemCount(itemStack.itemValue, -1, -1, true);
                    xuiC_FocusedBlockHealth.Text += $"\n{Localization.Get(itemName, false)} {hasCount}/{reqInt}";
                }
            }
        }
        [HarmonyPatch(typeof(XUiController), nameof(XUiController.RefreshBindings))]
        static class XUiController_RefreshBindings_Patch
        {
            static void Postfix(XUiController __instance)
            {
                if (!config.modEnabled || !config.showForBlocks || !(__instance is XUiC_FocusedBlockHealth))
                    return;
                __instance.WindowGroup.SetSize(160, 80);
                for(int i = 0; i < __instance.Children.Count; i++)
                {
                    if (__instance.Children[i].ViewComponent is XUiV_Label)
                    {
                        __instance.Children[i].ViewComponent.Size = new Vector2i(__instance.Children[i].ViewComponent.Size.x, 60);
                        break;
                    }
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
