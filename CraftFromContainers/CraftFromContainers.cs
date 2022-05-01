using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using UnityEngine;

namespace CraftFromContainers
{
    public class CraftFromContainers : IModApi
    {
        private static CraftFromContainers context;
        private static Mod mod;
        private static Dictionary<Vector3i, TileEntityLootContainer> storageDict = new Dictionary<Vector3i, TileEntityLootContainer>();
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        public static void Dbgl(object str, bool prefix = true)
        {
            Debug.Log((prefix ? mod.ModInfo.Name.Value : "") + str);
        }
        [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.HasItems))]
        static class XUiM_PlayerInventory_HasItems_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiM_PlayerInventory_HasItems");
                var codes = new List<CodeInstruction>(instructions);
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
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiM_PlayerInventory_RemoveItems");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Inventory), nameof(Inventory.DecItem)))
                    {
                        var ci = codes[i + 3];
                        var ciNew = new CodeInstruction(OpCodes.Ldarg_1);
                        ci.MoveLabelsTo(ciNew);
                        Dbgl("Adding method to remove from storages");
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.RemoveRemaining))));
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
                var codes = new List<CodeInstruction>(instructions);
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
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetItemCount), new Type[] { typeof(ItemValue) }))
                    {
                        Dbgl("Adding method to add item countss from all storages");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CraftFromContainers), nameof(CraftFromContainers.GetAllStoragesCount))));
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
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (i > 2 && codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(XUiC_RecipeList), "BuildRecipeInfosList"))
                    {
                        var ci = codes[i - 2];
                        var ciNew = new CodeInstruction(OpCodes.Ldloc_0);
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

        private static int GetAllStoragesCount(int count, XUiC_IngredientEntry entry)
        {
            ReloadStorages();

            if (storageDict.Count == 0)
                return count;
            foreach (var kvp in storageDict)
            {
                var items = kvp.Value.GetItems();
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j].itemValue.type == entry.Ingredient.itemValue.type)
                    {
                        count += items[j].count;
                    }
                }
            }
            return count;
        }
        
        private static ItemStack[] GetAllStorageStacks(ItemStack[] items)
        {
            ReloadStorages();

            if (storageDict.Count == 0)
                return items;

            List<ItemStack> itemList = new List<ItemStack>();
            itemList.AddRange(items);
            foreach (var kvp in storageDict)
            {
                itemList.AddRange(kvp.Value.GetItems());

            }
            return itemList.ToArray();
        }
        private static void AddAllStorageStacks(List<ItemStack> items)
        {
            ReloadStorages();

            if (storageDict.Count == 0)
                return;

            foreach (var kvp in storageDict)
            {
                items.AddRange(kvp.Value.GetItems());
            }
        }

        private static int GetTrueRemaining(IList<ItemStack> _itemStacks, int i, int numLeft)
        {
            ReloadStorages();

            if (storageDict.Count == 0)
                return numLeft;

            foreach (var kvp in storageDict)
            {
                var items = kvp.Value.GetItems();
                numLeft -= GetItemCount(items, _itemStacks[i].itemValue);
                if(numLeft <= 0)
                    return numLeft;
            }
            return numLeft;
        }
        
        private static void RemoveRemaining(IList<ItemStack> _itemStacks, int i, int numLeft)
        {
            ReloadStorages();

            if (storageDict.Count == 0)
                return;

            foreach (var kvp in storageDict)
            {
                var items = kvp.Value.GetItems();
                for (int j = 0; j < items.Length; j++)
                {
                    if (items[j].itemValue.type == _itemStacks[i].itemValue.type)
                    {
                        int toRem = Math.Min(numLeft, items[j].count);
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

        private static void ReloadStorages()
        {
            storageDict.Clear();
            for (int i = 0; i < GameManager.Instance.World.ChunkClusters.Count; i++)
            {
                var cc = GameManager.Instance.World.ChunkClusters[i];
                ReaderWriterLockSlim sync = (ReaderWriterLockSlim)AccessTools.Field(typeof(WorldChunkCache), "sync").GetValue(cc);
                sync.EnterReadLock();
                foreach (var c in cc.chunks.dict.Values)
                {
                    var cp = new Vector3i(c.X * 16, c.Y * 256, c.Z * 16);
                    DictionaryList<Vector3i, TileEntity> entities = (DictionaryList<Vector3i, TileEntity>)AccessTools.Field(typeof(Chunk), "tileEntities").GetValue(c);
                    foreach (var kvp in entities.dict)
                    {
                        if (kvp.Value is TileEntityLootContainer && (kvp.Value as TileEntityLootContainer).bPlayerStorage && !(kvp.Value as TileEntityLootContainer).IsUserAccessing())
                        {
                            var loc = cp + kvp.Value.localChunkPos;
                            storageDict[loc] = kvp.Value as TileEntityLootContainer;
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
                if ((!slots[i].itemValue.HasModSlots || !slots[i].itemValue.HasMods()) && slots[i].itemValue.type == _itemValue.type)
                {
                    num += slots[i].count;
                }
            }
            return num;
        }
    }
}
