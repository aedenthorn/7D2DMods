using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using UnityEngine;

namespace CraftFromContainers
{
    public class CraftFromContainers : IModApi
    {
        public static CraftFromContainers context;
        public static Mod mod;
        public static Dictionary<Vector3i, TEFeatureStorage> knownStorageDict = new Dictionary<Vector3i, TEFeatureStorage>();
        public static Dictionary<Vector3i, TEFeatureStorage> currentStorageDict = new Dictionary<Vector3i, TEFeatureStorage>();
        public static ModConfig config;

        public void InitMod(Mod modInstance)
        {
            Log.Out(" Loading Patch: " + GetType());
            LoadConfig();
            context = this;
            mod = modInstance;
            Harmony harmony = new HarmonyLib.Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void LoadConfig()
        {
            string path = Path.Combine(GetAssetPath(this, true), "config.json");
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
            if (config.isDebug)
                Debug.Log((prefix ? mod.DisplayName + " " : "") + str);
        }

        [HarmonyPatch(typeof(GameManager), "StartGame")]
        static class GameManager_StartGame_Patch
        {
            static void Prefix(bool _offline)
            {
                knownStorageDict.Clear();
            }
        }

        [HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]
        static class ItemActionEntryCraft_OnActivated_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling ItemActionEntryCraft.OnActivated");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetAllItemStacks)))
                    {
                        Dbgl("Adding method to add items from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.GetAllStorageStacks2))));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.HasItems))]
        static class XUiM_PlayerInventory_HasItems_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiM_PlayerInventory_HasItems");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (i > 0 && i < codes.Count - 1 && codes[i].opcode == OpCodes.Ldc_I4_0 && codes[i + 1].opcode == OpCodes.Ret)
                    {
                        Dbgl("Replacing return value with method");
                        codes.Insert(i, codes[i - 1].Clone());
                        codes.Insert(i, codes[i - 2].Clone());
                        codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.GetTrueRemaining))));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_1));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_0));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.RemoveItems))]
        static class XUiM_PlayerInventory_RemoveItems_Patch
        {
            public static void Prefix(IList<ItemStack> _itemStacks, int _multiplier)
            {
                for (int i = 0; i < _itemStacks.Count; i++)
                {
                    int num = _itemStacks[i].count * _multiplier;
                    Dbgl($"Need {num} {_itemStacks[i].itemValue.ItemClass.GetItemName()}");
                }
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiM_PlayerInventory_RemoveItems");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Inventory), nameof(Inventory.DecItem)))
                    {
                        CodeInstruction ci = codes[i + 3];
                        CodeInstruction ciNew = new CodeInstruction(OpCodes.Ldarg_1);
                        ci.MoveLabelsTo(ciNew);
                        Dbgl("Adding method to remove from storages");
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.RemoveRemainingForCraft))));
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldloc_1));
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldloc_0));
                        codes.Insert(i + 3, ciNew);
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(XUiC_RecipeCraftCount), "calcMaxCraftable")]
        static class XUiC_RecipeCraftCount_calcMaxCraftable_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiC_RecipeCraftCount_calcMaxCraftable");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetAllItemStacks)))
                    {
                        Dbgl("Adding method to add items from all storages");
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.GetAllStorageStacks))));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(XUiC_IngredientEntry), nameof(XUiC_IngredientEntry.GetBindingValue))]
        static class XUiC_IngredientEntry_GetBindingValue_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiC_IngredientEntry.GetBindingValue");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetItemCount), new Type[] { typeof(ItemValue) }))
                    {
                        Dbgl("Adding method to add item counts from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.AddAllStoragesCountEntry))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(XUiC_RecipeList), nameof(XUiC_RecipeList.Update))]
        static class XUiC_RecipeList_Update_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiC_RecipeList.Update");
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (i > 2 && codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(XUiC_RecipeList), "BuildRecipeInfosList"))
                    {
                        CodeInstruction ci = codes[i - 2];
                        CodeInstruction ciNew = new CodeInstruction(OpCodes.Ldloc_0);
                        ci.MoveLabelsTo(ciNew);
                        Dbgl("Adding method to add items from all storages");
                        codes.Insert(i - 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.AddAllStorageStacks))));
                        codes.Insert(i - 2, ciNew);
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "CanRemoveRequiredResource")]
        static class ItemActionRepair_CanRemoveRequiredResource_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                if (!config.enableForRepairAndUpgrade)
                    return codes;
                Dbgl("Transpiling ItemActionRepair.CanRemoveRequiredResource");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Bag), nameof(Bag.GetItemCount)))
                    {
                        Dbgl("Adding method to count items from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.AddAllStoragesCountItem))));
                        codes.Insert(i + 1, new CodeInstruction(codes[i - 4].opcode, codes[i - 4].operand));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "RemoveRequiredResource")]
        static class ItemActionRepair_RemoveRequiredResource_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                if (!config.enableForRepairAndUpgrade)
                    return codes;
                Dbgl("Transpiling ItemActionRepair.RemoveRequiredResource");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Bag), nameof(Bag.DecItem)))
                    {
                        Dbgl("Adding method to remove items from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.RemoveRemainingForUpgrade))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_0));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "canRemoveRequiredItem")]
        static class ItemActionRepair_canRemoveRequiredItem_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                if (!config.enableForRepairAndUpgrade)
                    return codes;
                Dbgl("Transpiling ItemActionRepair.canRemoveRequiredItem");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Bag), nameof(Bag.GetItemCount)))
                    {
                        Dbgl("Adding method to count items from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.AddAllStoragesCountItemStack))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_2));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "removeRequiredItem")]
        static class ItemActionRepair_removeRequiredItem_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
                if (!config.enableForRepairAndUpgrade)
                    return codes;
                Dbgl("Transpiling ItemActionRepair.removeRequiredItem");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Bag), nameof(Bag.DecItem)))
                    {
                        Dbgl("Adding method to remove items from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.RemoveRemainingForRepair))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_2));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        public static int AddAllStoragesCountEntry(int count, XUiC_IngredientEntry entry)
        {
            return AddAllStoragesCountItem(count, entry.Ingredient.itemValue);
        }
        public static int AddAllStoragesCountItemStack(int count, ItemStack itemStack)
        {
            return AddAllStoragesCountItem(count, itemStack.itemValue);
        }
        public static int AddAllStoragesCountItem(int count, ItemValue item)
        {
            ReloadStorages();
            if (currentStorageDict.Count == 0)
                return count;
            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                ItemStack[] items = kvp.Value.items;
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j].itemValue.type == item.type)
                    {
                        count += items[j].count;
                    }
                }
            }
            return count;
        }

        public static ItemStack[] GetAllStorageStacks(ItemStack[] items)
        {
            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return items;

            List<ItemStack> itemList = new List<ItemStack>();
            itemList.AddRange(items);
            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                itemList.AddRange(kvp.Value.items);

            }
            return itemList.ToArray();
        }
        public static List<ItemStack> GetAllStorageStacks2(List<ItemStack> items)
        {
            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return items;

            List<ItemStack> itemList = new List<ItemStack>();
            itemList.AddRange(items);
            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                itemList.AddRange(kvp.Value.items);

            }
            return itemList;
        }
        public static void AddAllStorageStacks(List<ItemStack> items)
        {
            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return;

            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                items.AddRange(kvp.Value.items);
            }
        }

        public static int GetTrueRemaining(IList<ItemStack> _itemStacks, int i, int numLeft)
        {
            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return numLeft;

            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                ItemStack[] items = kvp.Value.items;
                numLeft -= GetItemCount(items, _itemStacks[i].itemValue);
                if (numLeft <= 0)
                    return numLeft;
            }
            return numLeft;
        }

        public static void RemoveRemainingForCraft(IList<ItemStack> _itemStacks, int i, int numLeft)
        {
            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return;
            Dbgl($"Trying to remove {numLeft} {_itemStacks[i].itemValue.ItemClass.GetItemName()}");
            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                ItemStack[] items = kvp.Value.items;
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j].itemValue.type == _itemStacks[i].itemValue.type)
                    {
                        int toRem = Math.Min(numLeft, items[j].count);
                        Dbgl($"Removing {toRem}/{numLeft} {_itemStacks[i].itemValue.ItemClass.GetItemName()}");
                        numLeft -= toRem;
                        if (items[j].count <= toRem)
                            items[j].Clear();
                        else
                            items[j].count -= toRem;

                        kvp.Value.SetModified();
                        if (numLeft <= 0)
                            return;
                    }
                }
            }
        }

        public static int RemoveRemainingForUpgrade(int numRemoved, ItemActionRepair action, Block block)
        {
            if (!config.modEnabled)
                return numRemoved;
            int totalToRemove;
            if (!int.TryParse(block.Properties.Values[Block.PropUpgradeBlockClassItemCount], out totalToRemove))
            {
                return numRemoved;
            }
            ItemValue itemValue = ItemClass.GetItem(action.GetUpgradeItemName(block));
            if (totalToRemove <= numRemoved)
                return numRemoved;

            int numLeft = totalToRemove - numRemoved;

            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return numRemoved;

            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                ItemStack[] items = kvp.Value.items;
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j].itemValue.type == itemValue.type)
                    {
                        int toRem = Math.Min(numLeft, items[j].count);
                        numLeft -= toRem;
                        if (items[j].count <= toRem)
                            items[j].Clear();
                        else
                            items[j].count -= toRem;

                        kvp.Value.SetModified();
                        if (numLeft <= 0)
                            return totalToRemove;
                    }
                }
            }
            return totalToRemove - numLeft;
        }

        public static int RemoveRemainingForRepair(int numRemoved, ItemStack _itemStack)
        {
            if (!config.modEnabled)
                return numRemoved;
            int totalToRemove = _itemStack.count;

            if (totalToRemove <= numRemoved)
                return numRemoved;

            int numLeft = totalToRemove - numRemoved;

            ReloadStorages();

            if (currentStorageDict.Count == 0)
                return numRemoved;

            foreach (KeyValuePair<Vector3i, TEFeatureStorage> kvp in currentStorageDict)
            {
                ItemStack[] items = kvp.Value.items;
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j].itemValue.type == _itemStack.itemValue.type)
                    {
                        int toRem = Math.Min(numLeft, items[j].count);
                        numLeft -= toRem;
                        if (items[j].count <= toRem)
                            items[j].Clear();
                        else
                            items[j].count -= toRem;

                        kvp.Value.SetModified();
                        if (numLeft <= 0)
                            return totalToRemove;
                    }
                }
            }
            return totalToRemove - numLeft;
        }

        public static void ReloadStorages()
        {
            currentStorageDict.Clear();
            Vector3 pos = GameManager.Instance.World.GetPrimaryPlayer().position;
            for (int i = 0; i < GameManager.Instance.World.ChunkClusters.Count; i++)
            {
                ChunkCluster cc = GameManager.Instance.World.ChunkClusters[i];
                ReaderWriterLockSlim sync = (ReaderWriterLockSlim)AccessTools.Field(typeof(WorldChunkCache), "sync").GetValue(cc);
                sync.EnterReadLock();
                foreach (Chunk c in cc.chunks.dict.Values)
                {
                    DictionaryList<Vector3i, TileEntity> entities = (DictionaryList<Vector3i, TileEntity>)AccessTools.Field(typeof(Chunk), "tileEntities").GetValue(c);
                    foreach (KeyValuePair<Vector3i, TileEntity> kvp in entities.dict)
                    {
                        Vector3i loc = kvp.Value.ToWorldPos();
                        if (!kvp.Value.TryGetSelfOrFeature<TEFeatureStorage>(out TEFeatureStorage lootTileEntity))
                            continue;
                        if (lootTileEntity.bPlayerStorage && !lootTileEntity.IsUserAccessing() && (!lootTileEntity.lockFeature.IsLocked() || lootTileEntity.lockFeature.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier)))
                        {
                            knownStorageDict[loc] = lootTileEntity;
                            if (config.range <= 0 || Vector3.Distance(pos, loc) < config.range)
                                currentStorageDict[loc] = lootTileEntity;
                        }
                    }
                }
                sync.ExitReadLock();
            }
        }
        public static int GetItemCount(ItemStack[] slots, ItemValue _itemValue)
        {
            int num = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].itemValue.type == _itemValue.type)
                {
                    num += slots[i].count;
                }
            }
            return num;
        }
    }
}
