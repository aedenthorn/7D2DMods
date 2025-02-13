using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using Path = System.IO.Path;

namespace QuickStorage
{
    public class QuickStorage : IModApi
    {

        public static ModConfig config;
        public static QuickStorage context;
        public static Mod mod;
        private static List<Vector3i> storageList = new List<Vector3i>();
        private static Dictionary<Vector3i, TEFeatureStorage> storageDict = new Dictionary<Vector3i, TEFeatureStorage>();
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

        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null || ___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsModalWindowOpen())
                    return;

                if(config.storeKey == config.pullKey)
                {
                    if (AedenthornUtils.CheckKeyDown(config.storeKey))
                    {
                        if(string.IsNullOrEmpty(config.storeModKey))
                        {
                            if (AedenthornUtils.CheckKeyHeld(config.pullModKey))
                            {
                                Dbgl($"Pressed pull key");
                                PullItems(___m_World);
                            }
                            else
                            {
                                Dbgl($"Pressed store key");
                                StoreItems(___m_World);
                            }
                        }
                        else if (AedenthornUtils.CheckKeyDown(config.storeModKey))
                        {
                            Dbgl($"Pressed store key");
                            StoreItems(___m_World);
                        }
                        else if ( AedenthornUtils.CheckKeyHeld(config.pullModKey))
                        {
                            Dbgl($"Pressed pull key");
                            PullItems(___m_World);
                        }
                    }
                }
                else if (AedenthornUtils.CheckKeyDown(config.storeKey) && AedenthornUtils.CheckKeyHeld(config.storeModKey, false))
                {
                    Dbgl($"Pressed store key");
                    StoreItems(___m_World);
                }
                else if (AedenthornUtils.CheckKeyDown(config.pullKey) && AedenthornUtils.CheckKeyHeld(config.pullModKey, false))
                {
                    Dbgl($"Pressed pull key");
                    PullItems(___m_World);
                }
            }
        }
        private static void StoreItems(World world)
        {
            Dbgl($"Storing items");
            LoadConfig();
            storageList.Clear();
            storageDict.Clear();

            for (int i = 0; i < world.ChunkClusters.Count; i++)
            {

                var cc = world.ChunkClusters[i];

                foreach (var c in cc.chunks.dict.Values)
                {
                    c.EnterReadLock();
                    foreach (var kvp in c.tileEntities.dict)
                    {
                        var loc = kvp.Value.ToWorldPos();
                        if (config.range < 0 || Vector3.Distance(world.GetPrimaryPlayer().position, loc) > config.range)
                            continue;
                        var entity = (kvp.Value as TileEntityComposite);
                        if (entity != null)
                        {
                            var lootable = entity.GetFeature<ITileEntityLootable>() as TEFeatureStorage;
                            if (lootable != null && lootable.bPlayerStorage)
                            {
                                var lockable = entity.GetFeature<ILockable>();
                                if (lockable == null || !lockable.IsLocked() || lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier))
                                {
                                    storageList.Add(loc);
                                    storageDict.Add(loc, lootable);

                                }

                            }

                        }
                    }
                    c.ExitReadLock();

                }
            }
            var pos = world.GetPrimaryPlayer().position;
            storageList.Sort(delegate (Vector3i a, Vector3i b) {
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
            Dbgl($"Got {storageList.Count} storages"); 
            
            Dictionary<int, int> dict = new Dictionary<int, int>();
            var player = world.GetPrimaryPlayer();
            var bag = player.bag;
            var ctrl = ((XUiWindowGroup)player.PlayerUI.windowManager.GetWindow("backpack")).Controller.GetChildByType<XUiC_Backpack>();
            ItemStack[] slots = bag.GetSlots();
            for (int i = config.skipSlots; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty() || (bag.LockedSlots.Length > i && bag.LockedSlots[i]))
                    continue;
                var initItem = slots[i].Clone();
                var itemName = ItemClass.GetForId(initItem.itemValue.type).Name;
                if (config.storeIgnore.Length > 0)
                {
                    foreach (var s in config.storeIgnore)
                    {
                        if ((s.EndsWith("*") && itemName.StartsWith(s.Substring(0, s.Length - 1))) || itemName.Equals(s))
                        {
                            Dbgl($"Ignoring {itemName} from config");
                            goto next;
                        }
                    }
                }
                foreach (var v in storageList)
                {
                    if (Array.Exists(storageDict[v].items, s => s.itemValue.type == initItem.itemValue.type))
                    {
                        storageDict[v].TryStackItem(0, slots[i]);
                        if (slots[i].count > 0)
                        {
                            if (storageDict[v].AddItem(slots[i]))
                                slots[i].count = 0;
                        }
                        int moved = initItem.count - slots[i].count;
                        if(moved > 0)
                        {
                            bag.onBackpackChanged();
                            storageDict[v].SetModified();
                            if (dict.ContainsKey(initItem.itemValue.type))
                            {
                                dict[initItem.itemValue.type] += moved;
                            }
                            else
                            {
                                dict[initItem.itemValue.type] = moved;
                            }
                        }
                        if (slots[i].count == 0)
                        {
                            slots[i].Clear();
                            break;
                        }
                    }
                }
            next:
                continue;
            }

            foreach (var kvp in dict)
            {
                var itemName = ItemClass.GetForId(kvp.Key).Name;

                Dbgl($"Stored {kvp.Value} of item {itemName}");
                world.GetPrimaryPlayer().AddUIHarvestingItem(new ItemStack(new ItemValue(kvp.Key), -kvp.Value), false);
            }

            Dbgl($"Stored {dict.Count} items");
        }
        private static void PullItems(World world)
        {
            Dbgl($"Pulling items");
            LoadConfig();
            storageList.Clear();
            storageDict.Clear();

            for (int i = 0; i < world.ChunkClusters.Count; i++)
            {

                var cc = world.ChunkClusters[i];

                foreach (var c in cc.chunks.dict.Values)
                {
                    c.EnterReadLock();
                    foreach (var kvp in c.tileEntities.dict)
                    {
                        var loc = kvp.Value.ToWorldPos();
                        var entity = (kvp.Value as TileEntityComposite);
                        if (entity != null)
                        {
                            var lootable = entity.GetFeature<ITileEntityLootable>() as TEFeatureStorage;
                            if (lootable != null && lootable.bPlayerStorage)
                            {
                                var lockable = entity.GetFeature<ILockable>();
                                if (lockable == null || !lockable.IsLocked() || lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier))
                                {
                                    storageList.Add(loc);
                                    storageDict.Add(loc, lootable);

                                }

                            }

                        }
                    }
                    c.ExitReadLock();

                }
            }
            var pos = world.GetPrimaryPlayer().position;
            storageList.Sort(delegate (Vector3i a, Vector3i b) {
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
            Dbgl($"Got {storageList.Count} storages");
            
            Dictionary<int, int> dict = new Dictionary<int, int>();
            var bag = world.GetPrimaryPlayer().bag;
            var slots = bag.GetSlots();
            var toolbelt = world.GetPrimaryPlayer().inventory;
            var tslots = toolbelt.GetSlots();
            
            for (int i = 0; i < slots.Length + tslots.Length; i++)
            {
                int idx = i;
                ItemStack slot;
                if(idx < slots.Length)
                    slot = slots[idx];
                else
                {
                    idx -= slots.Length;
                    slot = tslots[idx];
                }
                if (slot.IsEmpty() || ItemClass.GetForId(slot.itemValue.type).Stacknumber.Value == slot.count)
                    continue;

                var itemName = ItemClass.GetForId(slot.itemValue.type).Name;

                if (config.pullIgnore.Length > 0)
                {
                    foreach (var s in config.pullIgnore)
                    {
                        if ((s.EndsWith("*") && itemName.StartsWith(s.Substring(0, s.Length - 1))) || itemName.Equals(s))
                        {
                            Dbgl($"Ignoring {itemName} from config");
                            goto next;
                        }
                    }
                }
                foreach (var v in storageList)
                {
                    for (int j = storageDict[v].items.Length - 1; j >= 0; j--)
                    {
                        var item = storageDict[v].items[j];
                        if (item.IsEmpty())
                            continue;

                        var initItem = storageDict[v].items[j].Clone();
                        int num = storageDict[v].items[j].count;
                        if (storageDict[v].items[j].itemValue.type == slot.itemValue.type && slot.CanStackPartly(ref num))
                        {
                            storageDict[v].items[j].count -= num;
                            if(i < slots.Length)
                            {
                                slots[idx].count += num;
                                bag.onBackpackChanged();
                            }
                            else
                            {
                                tslots[idx].count += num;
                                toolbelt.onInventoryChanged();
                            }
                            storageDict[v].SetModified();
                            int moved = initItem.count - storageDict[v].items[j].count;
                            if (dict.ContainsKey(initItem.itemValue.type))
                            {
                                dict[initItem.itemValue.type] += moved;
                            }
                            else
                            {
                                dict[initItem.itemValue.type] = moved;
                            }
                        }
                    }
                }
            next:
                continue;
            }
            foreach(var kvp in dict)
            {
                var itemName = ItemClass.GetForId(kvp.Key).Name;

                Dbgl($"Pulled {kvp.Value} of item {itemName}");
                world.GetPrimaryPlayer().AddUIHarvestingItem(new ItemStack(new ItemValue(kvp.Key), kvp.Value), false);
            }

            Dbgl($"Pulled {dict.Count} items");
        }
    }
}
