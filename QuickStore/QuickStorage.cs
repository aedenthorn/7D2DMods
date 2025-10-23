using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace QuickStorage
{
    public class QuickStorage : IModApi
    {

        public static ModConfig config;
        public static QuickStorage context;
        public static Mod mod;
        public static List<Vector3i> storageList = new List<Vector3i>();
        public static HashSet<Vector3i> lockedList = new HashSet<Vector3i>();
        public static Dictionary<Vector3i, ITileEntityLootable> storageDict = new Dictionary<Vector3i, ITileEntityLootable>();
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

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TELockServer))]
        public static class GameManager_TELockServer_Patch
        {
            public static void Postfix(GameManager __instance, int _clrIdx, Vector3i _blockPos, int _lootEntityId)
            {
                if (!config.modEnabled || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                    return;

                TileEntity tileEntity;
                if (_lootEntityId == -1)
                {
                    tileEntity = __instance.m_World.GetTileEntity(_blockPos);
                }
                else
                {
                    tileEntity = __instance.m_World.GetTileEntity(_lootEntityId);
                }
                if (tileEntity == null)
                {
                    return;
                }
                if (__instance.lockedTileEntities.ContainsKey(tileEntity))
                {
                    Dbgl($"Sending locked message");

                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuickStoreLock>().Setup(_blockPos, false), true, -1, -1, -1, null, 192, false);
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.TEUnlockServer))]
        public static class GameManager_TEUnlockServer_Patch
        {
            public static void Postfix(GameManager __instance, int _clrIdx, Vector3i _blockPos, int _lootEntityId)
            {
                if (!config.modEnabled || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                    return;

                TileEntity tileEntity;
                if (_lootEntityId == -1)
                {
                    tileEntity = __instance.m_World.GetTileEntity(_blockPos);
                }
                else
                {
                    tileEntity = __instance.m_World.GetTileEntity(_lootEntityId);
                }
                if (tileEntity == null)
                {
                    return;
                }
                if (!__instance.lockedTileEntities.ContainsKey(tileEntity))
                {
                    Dbgl($"Sending unlocked message");

                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuickStoreLock>().Setup(_blockPos, true), true, -1, -1, -1, null, 192, false);
                }
            }
        }


        [HarmonyPatch(typeof(GameManager), "Update")]
        public static class GameManager_Update_Patch
        {

            public static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World?.GetPrimaryPlayer()?.PlayerUI?.windowManager?.IsModalWindowOpen() != false)
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
        public static void StoreItems(World world)
        {
            var player = world?.GetPrimaryPlayer();
            if (player is null)
                return;
            Dbgl($"Storing items");
            LoadConfig();
            storageList.Clear();
            storageDict.Clear();

            for (int i = 0; i < world.ChunkClusters.Count; i++)
            {

                var cc = world.ChunkClusters[i];

                foreach(var key in cc.chunks.dict.Keys.ToArray())
                {
                    if (!cc.chunks.dict.TryGetValue(key, out var c))
                        continue;
                    c.EnterReadLock();
                    foreach (var key2 in c.tileEntities.dict.Keys.ToArray())
                    {
                        if (!c.tileEntities.dict.TryGetValue(key2, out var tileEntity))
                            continue;

                        var loc = tileEntity.ToWorldPos();
                        if (config.range >= 0 && Vector3.Distance(player.position, loc) > config.range)
                            continue;
                        var entity = (tileEntity as TileEntityComposite);
                        if (entity != null)
                        {
                            var lootable = entity.GetFeature<ITileEntityLootable>();
                            if (lootable != null && lootable.bPlayerStorage)
                            {
                                var lockable = entity.GetFeature<ILockable>();
                                if (lockable == null || !lockable.IsLocked() || lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier))
                                {
                                    if (lockedList.Contains(loc))
                                    {
                                        Dbgl($"storage is locked!");
                                        continue;
                                    }

                                    if (!entity.IsUserAccessing() && !GameManager.Instance.lockedTileEntities.ContainsKey(tileEntity))
                                    {
                                        storageList.Add(loc);
                                        storageDict.Add(loc, lootable);
                                    }
                                }
                            }
                        }
                    }
                    c.ExitReadLock();

                }
            }
            var pos = player.position;
            if (!storageList.Any())
                return;
            storageList.Sort(delegate (Vector3i a, Vector3i b) {
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
            Dbgl($"Got {storageList.Count} storages"); 
            
            Dictionary<int, int> dict = new Dictionary<int, int>();
            var bag = player.bag;
            if(bag is null)
            {
                Dbgl($"Player bag is null");
                return;
            }
            ItemStack[] slots = bag.GetSlots();
            for (int i = config.skipSlots; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty() || (bag.LockedSlots.Length > i && bag.LockedSlots[i]))
                    continue;
                var initItem = slots[i].Clone();
                if (initItem is null)
                    continue;
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

            foreach (var key in dict.Keys.ToArray())
            {
                if (!dict.TryGetValue(key, out var value))
                    continue;
                var itemName = ItemClass.GetForId(key).Name;

                Dbgl($"Stored {value} of item {itemName}");
                world.GetPrimaryPlayer().AddUIHarvestingItem(new ItemStack(new ItemValue(key), -value), false);
            }

            Dbgl($"Stored {dict.Count} items");
        }
        public static void PullItems(World world)
        {
            Dbgl($"Pulling items");
            LoadConfig();
            storageList.Clear();
            storageDict.Clear();

            for (int i = 0; i < world.ChunkClusters.Count; i++)
            {

                var cc = world.ChunkClusters[i];

                foreach (var key in cc.chunks.dict.Keys.ToArray())
                {
                    if (!cc.chunks.dict.TryGetValue(key, out var c))
                        continue;
                    c.EnterReadLock();
                    foreach (var key2 in c.tileEntities.dict.Keys.ToArray())
                    {
                        if (!c.tileEntities.dict.TryGetValue(key2, out var tileEntity))
                            continue;
                        var loc = tileEntity.ToWorldPos();
                        var entity = (tileEntity as TileEntityComposite);
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
            foreach (var key in dict.Keys.ToArray())
            {
                if (!dict.TryGetValue(key, out var value))
                    continue;
                var itemName = ItemClass.GetForId(key).Name;

                Dbgl($"Pulled {value} of item {itemName}");
                world.GetPrimaryPlayer().AddUIHarvestingItem(new ItemStack(new ItemValue(key), value), false);
            }

            Dbgl($"Pulled {dict.Count} items");
        }
    }
}
