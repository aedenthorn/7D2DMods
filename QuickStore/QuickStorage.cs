using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using Path = System.IO.Path;

namespace QuickStore
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

                if (AedenthornUtils.CheckKeyDown(config.storeKey))
                {
                    Dbgl($"Pressed store key");
                    if (AedenthornUtils.CheckKeyHeld(config.modKey))
                    {
                        Dbgl($"mod key held");
                        PullItems(___m_World);
                    }
                    else
                    {
                        StoreItems(___m_World);
                    }
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
            int count = 0;
            var items = world.GetPrimaryPlayer().bag.GetSlots();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].IsEmpty())
                    continue;
                var initItem = items[i].Clone(); 
                var itemName = ItemClass.GetForId(initItem.itemValue.type).Name;
                if (config.storeIgnore.FirstOrDefault(s => s.Equals(itemName)) != null)
                {
                    Dbgl($"Ignoring {itemName} from config");
                    continue;
                }
                foreach (var v in storageList)
                {
                    if (Array.Exists(storageDict[v].items, e => e.itemValue.type == items[i].itemValue.type))
                    {
                        if ((storageDict[v].TryStackItem(0, items[i]).anyMoved && items[i].count == 0) || storageDict[v].AddItem(items[i]))
                        {
                            items[i].Clear();
                            storageDict[v].SetModified();
                            count++;
                        }
                        if (initItem.count > items[i].count)
                        {
                            storageDict[v].SetModified();
                        }
                        if (items[i].IsEmpty())
                            break;
                    }
                }
                if (items[i].count < initItem.count)
                {
                    Dbgl($"Stored {initItem.count - items[i].count} of item {itemName}");

                    world.GetPrimaryPlayer().AddUIHarvestingItem(new ItemStack(initItem.itemValue, items[i].count - initItem.count), false);
                }
            }
            Dbgl($"Stored {count} items");
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
            int count = 0;
            var bag = world.GetPrimaryPlayer().bag;
            var items = bag.GetSlots();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].IsEmpty())
                    continue;
                var initItem = items[i].Clone();
                var itemName = ItemClass.GetForId(initItem.itemValue.type).Name;
                if (config.pullIgnore.FirstOrDefault(s => s.Equals(itemName)) != null)
                {
                    Dbgl($"Ignoring {itemName} from config");
                    continue;
                }
                foreach (var v in storageList)
                {
                    var slots = storageDict[v].items.Where(stack => stack.CanStackPartlyWith(items[i]));
                    if (slots.Any())
                    {
                        for(int j = 0; j < slots.Count(); j++)
                        {
                            if (!bag.TryStackItem(0, slots.ElementAt(j)).anyMoved)
                                goto next;
                        }
                    }
                }
            next:
                if (items[i].count > initItem.count)
                {
                    Dbgl($"Pulled {items[i].count - initItem.count} of item {itemName}");
                    world.GetPrimaryPlayer().AddUIHarvestingItem(new ItemStack(initItem.itemValue, items[i].count - initItem.count), false);
                }
            }
            Dbgl($"Stored {count} items");
        }
    }
}
