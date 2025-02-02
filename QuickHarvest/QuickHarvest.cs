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

namespace QuickHarvest
{
    public class QuickHarvest : IModApi
    {

        public static ModConfig config;
        public static QuickHarvest context;
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

        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null || LocalPlayerUI.AnyModalWindowOpen())
                    return;
                if (AedenthornUtils.CheckKeyDown(config.harvestKey) && AedenthornUtils.CheckKeyHeld(config.harvestModKey, false))
                {
                    Dbgl($"Pressed harvest key");
                    HarvestCrops(___m_World.GetPrimaryPlayer());
                }
            }
        }
        private static void HarvestCrops(EntityPlayerLocal player)
        {
            Dbgl($"Harvesting crops");
            LoadConfig();

            int count = 0;
            Vector3i blockPosition = player.GetBlockPosition();

            var radius = config.blockRadius;
            GameUtils.random = GameRandomManager.Instance.CreateGameRandom();
            GameUtils.random.SetSeed((int)Stopwatch.GetTimestamp());
            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    for (int k = -radius; k <= radius; k++)
                    {
                        var newPos = blockPosition + new Vector3i(i, j, k);
                        BlockValue bv = player.world.GetBlock(newPos);
                        if (bv.isair || bv.ischild)
                            continue;
                        if (bv.Block.blockName.ToLower().StartsWith("planted") && bv.Block.blockName.ToLower().EndsWith("harvestplayer"))
                        {
                            try
                            {
                                BlockValue newBlock = BlockValue.Air;
                                ItemValue seedValue = null;
                                if (bv.Block.itemsToDrop.TryGetValue(EnumDropEvent.Destroy, out var list))
                                {
                                    for (int x = 0; x < list.Count; x++)
                                    {
                                        var item = list[x];
                                        if (!item.name.StartsWith("planted"))
                                            continue;
                                        float num2 = EffectManager.GetValue(PassiveEffects.HarvestCount, player.inventory.holdingItemItemValue, 1, player, null, FastTags<TagGroup.Global>.Parse(item.tag), true, true, true, true, true, 1, true, false);

                                        seedValue = item.name.Equals("*") ? bv.ToItemValue() : new ItemValue(ItemClass.GetItem(item.name, false).type, false);
                                        if (seedValue.type != 0 && ItemClass.list[seedValue.type] != null && (item.prob > 0.999f || GameUtils.random.RandomFloat <= item.prob))
                                        {
                                            int num3 = (int)((float)GameUtils.random.RandomRange(item.minCount, item.maxCount + 1) * num2);
                                            if (num3 > 0)
                                            {
                                                if(newBlock.Equals(BlockValue.Air))
                                                {
                                                    newBlock = seedValue.ToBlockValue();
                                                    if (!newBlock.Equals(BlockValue.Air))
                                                    {
                                                        Dbgl($"got replacement seed {newBlock.Block.blockName}");
                                                        num3--;
                                                    }
                                                }
                                                if (num3 > 0)
                                                {
                                                    collectHarvestedItem(player, bv, seedValue, num3);
                                                }

                                            }
                                        }
                                    }
                                }
                                if (bv.Block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest, out var list2))
                                {
                                    for (int x = 0; x < list2.Count; x++)
                                    {
                                        int itemsToCollect = 0;
                                        var item = list2[x];
                                        float num4 = 0f;
                                        ItemValue itemValue = (item.name.Equals("*") ? bv.ToItemValue() : new ItemValue(ItemClass.GetItem(item.name, false).type, false));
                                        if (itemValue.type != 0 && ItemClass.list[itemValue.type] != null)
                                        {
                                            num4 = EffectManager.GetValue(PassiveEffects.HarvestCount, player.inventory.holdingItemItemValue, num4, player, null, FastTags<TagGroup.Global>.Parse(item.tag), true, true, true, true, true, 1, true, false);
                                            int num5 = (int)((float)GameUtils.random.RandomRange(item.minCount, item.maxCount + 1) * num4);
                                            int num6 = num5 - num5 / 3;
                                            if (num6 > 0)
                                            {
                                                if(GameUtils.random.RandomFloat <= item.prob)
                                                {
                                                    itemsToCollect += num6;
                                                }
                                            }
                                            num6 = num5 / 3;
                                            float num7 = item.prob;
                                            float resourceScale = item.resourceScale;
                                            if (resourceScale > 0f && resourceScale < 1f)
                                            {
                                                num7 /= resourceScale;
                                                num6 = (int)((float)num6 * resourceScale);
                                                if (num6 < 1)
                                                {
                                                    num6++;
                                                }
                                            }
                                            if (num6 > 0)
                                            {
                                                if (GameUtils.random.RandomFloat <= num7)
                                                {
                                                    itemsToCollect += num6;
                                                }
                                            }
                                            if (newBlock.Equals(BlockValue.Air) && seedValue != null)
                                            {
                                                if(itemValue.type == seedValue.type)
                                                {
                                                    newBlock = itemValue.ToBlockValue();
                                                    Dbgl($"got replacement seed {newBlock.Block.blockName}");
                                                    itemsToCollect--;

                                                }
                                                else
                                                {
                                                    foreach (var r in CraftingManager.GetRecipes(seedValue.ItemClass.Name))
                                                    {
                                                        if (!r.IsUnlocked(player))
                                                            continue;
                                                        //Dbgl($"got replacement {seedValue.ItemClass.Name} recipe with {r.ingredients.Count} ing; first is {r.ingredients[0].count} {r.ingredients[0].itemValue.ItemClass.Name}");
                                                        if (r.ingredients.Count == 1 && r.ingredients[0].itemValue.type == itemValue.type && itemsToCollect >= r.ingredients[0].count)
                                                        {

                                                            newBlock = seedValue.ToBlockValue();
                                                            if (!newBlock.Equals(BlockValue.Air))
                                                            {
                                                                Dbgl($"got replacement {newBlock.Block.blockName} recipe for {r.ingredients[0].count} {r.ingredients[0].itemValue.ItemClass.Name}");
                                                                itemsToCollect -= r.ingredients[0].count;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            if(itemsToCollect > 0)
                                            {
                                                collectHarvestedItem(player, bv, itemValue, itemsToCollect);
                                            }
                                        }
                                    }
                                }
                                newBlock.rotation = bv.rotation;
                                if (config.replaceCrop && !newBlock.Equals(BlockValue.Air))
                                {
                                    Dbgl($"replacing crop with {newBlock.Block.blockName}");
                                    GameManager.Instance.SetBlocksRPC(new List<BlockChangeInfo> {  new BlockChangeInfo
                                    {
                                        pos = newPos,
                                        clrIdx = 0,
                                        bChangeBlockValue = true,
                                        blockValue = newBlock
                                    } });
                                }
                                else
                                {
                                    bv.Block.DamageBlock(player.world, 0, newPos, bv, 1, player.entityId);
                                }
                                count++;
                            }
                            catch (Exception e) 
                            {
                            }
                        }
                    }
                }
            }

            Dbgl($"Harvested {count} crops");
        }

        private static void collectHarvestedItem(EntityPlayerLocal player, BlockValue bv, ItemValue _iv, int _count)
        {
            if (_count > 0)
            {
                ItemStack itemStack = new ItemStack(_iv, _count);
                LocalPlayerUI uiforPlayer = LocalPlayerUI.GetUIForPlayer(player);
                XUiM_PlayerInventory playerInventory = uiforPlayer.xui.PlayerInventory;
                QuestEventManager.Current.HarvestedItem(player.inventory.holdingItemItemValue, itemStack, bv);
                if (!playerInventory.AddItem(itemStack))
                {
                    GameManager.Instance.ItemDropServer(new ItemStack(_iv, itemStack.count), GameManager.Instance.World.GetPrimaryPlayer().GetDropPosition(), new Vector3(0.5f, 0.5f, 0.5f), GameManager.Instance.World.GetPrimaryPlayerId(), 60f, false);
                }
                uiforPlayer.entityPlayer.Progression.AddLevelExp((int)(itemStack.itemValue.ItemClass.MadeOfMaterial.Experience * (float)_count), "_xpFromHarvesting", Progression.XPTypes.Harvesting, true, true);
            }

        }
    }
}
