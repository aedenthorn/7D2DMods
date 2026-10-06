using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Webserver.WebAPI.APIs.WorldState;
using static AIDirectorPlayerInventory;
using Path = System.IO.Path;

namespace QuickStorage
{
    public class QuickStorage : IModApi
    {

        public static ModConfig config;
        public static QuickStorage context;
        public static Mod mod;
        public static List<Vector3i> storageList = new List<Vector3i>();
        public static Dictionary<Vector3i, object> storageDict = new Dictionary<Vector3i, object>();
        public static HashSet<ILockTarget> lockedList = new HashSet<ILockTarget>();

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

        [HarmonyPatch(typeof(ConnectionManager), nameof(ConnectionManager.SendPackage), new Type[] { typeof(NetPackage), typeof(bool), typeof(int), typeof(int), typeof(int), typeof(Vector3?), typeof(int), typeof(bool) })]
        public static class ConnectionManager_SendPackage_Patch
        {
            public static void Postfix(ConnectionManager __instance, NetPackage _package, bool _onlyClientsAttachedToAnEntity)
            {
                if (!config.modEnabled || !__instance.IsServer || !(_package is NetPackageLockTarget package))
                    return;
                using (PooledBinaryReader pooledBinaryReader = MemoryPools.poolBinaryReader.AllocSync(false))
                {
                    PooledExpandableMemoryStream pooledExpandableMemoryStream1 = package.payload;
                    lock (pooledExpandableMemoryStream1)
                    {
                        pooledBinaryReader.SetBaseStream(package.payload);
                        package.payload.Position = 0L;
                        bool locking = pooledBinaryReader.ReadBoolean();
                        bool success = pooledBinaryReader.ReadBoolean();
                        if(!success)
                            return;
                        string error = pooledBinaryReader.ReadString();
                        ushort channel = pooledBinaryReader.ReadUInt16();
                        int length = pooledBinaryReader.ReadInt32();
                        ILockTarget[] array = new ILockTarget[length];
                        for (int i = 0; i < length; i++)
                        {
                            array[i] = WorldAddress.Read(pooledBinaryReader).Resolve<ILockTarget>();
                        }
                        package.payload.Position = 0L;

                        PooledExpandableMemoryStream pooledExpandableMemoryStream = MemoryPools.poolMemoryStream.AllocSync(true);
                        try
                        {
                            using (PooledBinaryWriter pooledBinaryWriter = MemoryPools.poolBinaryWriter.AllocSync(false))
                            {
                                pooledBinaryWriter.SetBaseStream(pooledExpandableMemoryStream);
                                pooledBinaryWriter.Write(locking);
                                pooledBinaryWriter.Write(length);
                                for (int m = 0; m < array.Length; m++)
                                {
                                    WorldAddress.Create(array[m]).Write(pooledBinaryWriter);
                                }
                            }

                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuickStoreLock>().Setup(pooledExpandableMemoryStream));
                        }
                        finally
                        {
                            MemoryPools.poolMemoryStream.FreeSync(pooledExpandableMemoryStream);
                        }

                    }
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

            foreach (var c in world.ChunkCache.chunks.list)
            {
                c.EnterReadLock();
                foreach (var key2 in c.tileEntities.dict.Keys.ToArray())
                {
                    if (!c.tileEntities.dict.TryGetValue(key2, out var tileEntity))
                        continue;

                    var loc = tileEntity.ToWorldPos();
                    if (config.range >= 0 && Vector3.Distance(player.position, loc) > config.range)
                        continue;
                    if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ? LockManager.Instance.IsLockedServer(tileEntity, 0) : LockManager.Instance.IsLockedByLocalPlayer(tileEntity, 0))
                        continue;
                    var entity = (tileEntity as TileEntityComposite);
                    if (entity != null)
                    {
                        var lootable = entity.GetFeature<TEFeatureStorage>();
                        if (lootable != null && lootable.ItemGrid.PlayerOwned)
                        {
                            var lockable = entity.GetFeature<ILockable>();
                            if (lockable == null || !lockable.IsLocked() || lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier) && !lockedList.Contains(lootable))
                            {
                                if (!entity.IsUserAccessing())
                                {
                                    storageList.Add(loc);
                                    storageDict.Add(loc, lootable);
                                }
                            }
                        }
                        continue;
                    }
                    var entity2 = (tileEntity as TileEntityForge);
                    if (entity2 != null)
                    {

                        if (!entity2.IsUserAccessing())
                        {
                            storageList.Add(loc);
                            storageDict.Add(loc, entity2);
                        }
                        continue;
                    }
                }
                c.ExitReadLock();
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
            if(bag == null)
            {
                Dbgl($"Player bag is null");
                return;
            }
            ItemStack[] slots = bag.ItemGrid.items;
            for (int i = config.skipSlots; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].IsEmpty() || (bag.LockedSlots?.Length > i && bag.LockedSlots[i]))
                    continue;
                var initItem = slots[i].Clone();
                if (initItem == null)
                    continue;
                string itemName = ItemClass.GetForId(initItem.itemValue.type)?.Name;
                if (itemName == null)
                    continue;
                if (config.storeIgnore?.Length > 0)
                {
                    foreach (var s in config.storeIgnore)
                    {
                        if ((s.EndsWith("*") && itemName.StartsWith(s.Substring(0, s.Length - 1))) || itemName == s)
                        {
                            Dbgl($"Ignoring {itemName} from config");
                            goto next;
                        }
                    }
                }
                foreach (var v in storageList)
                {
                    if (!storageDict.TryGetValue(v, out var obj))
                        continue;
                    if(obj is TEFeatureStorage tel && tel.ItemGrid.items != null)
                    {

                        if (Array.Exists(tel.ItemGrid.items, s => s.itemValue.type == initItem.itemValue.type))
                        {
                            tel.TryStackItem(0, slots[i]);
                            if (slots[i].count > 0)
                            {
                                if (tel.AddItem(slots[i]))
                                    slots[i].count = 0;
                            }
                            int moved = initItem.count - slots[i].count;
                            if (moved > 0)
                            {
                                bag.onBackpackChanged();
                                tel.SetModified();
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
                    else if (obj is TileEntityForge tef && tef.input != null)
                    {
                        if (Array.Exists(tef.input, s => s.itemValue.type == initItem.itemValue.type))
                        {
                            int moved = TryStackItem(tef.input, 0, slots[i]);
                            if (moved > 0)
                            {
                                bag.onBackpackChanged();
                                tef.SetModified();
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
                player.AddUIHarvestingItem(new ItemStack(new ItemValue(key), -value), false);
            }

            Dbgl($"Stored {dict.Count} items");
        }

        public static int TryStackItem(ItemStack[] items, int startIndex, ItemStack _itemStack)
        {
            int count = _itemStack.count;
            int num = 0;
            int total = 0;
            for (int i = startIndex; i < items.Length; i++)
            {
                num = _itemStack.count;
                if (_itemStack.itemValue.type == items[i].itemValue.type && items[i].CanStackPartly(ref num))
                {
                    items[i].count += num;
                    _itemStack.count -= num;
                    total += num;
                    if (_itemStack.count == 0)
                    {
                        return total;
                    }
                }
            }
            return total;
        }

        public static void PullItems(World world)
        {
            var player = world?.GetPrimaryPlayer();
            if (player is null)
                return;
            Dbgl($"Pulling items");
            LoadConfig();
            storageList.Clear();
            storageDict.Clear();

            foreach (var c in world.ChunkCache.chunks.list)
            {


                c.EnterReadLock();
                foreach (var key2 in c.tileEntities.dict.Keys.ToArray())
                {
                    if (!c.tileEntities.dict.TryGetValue(key2, out var tileEntity))
                        continue;
                    if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer ? LockManager.Instance.IsLockedServer(tileEntity, 0) : LockManager.Instance.IsLockedByLocalPlayer(tileEntity, 0))
                        continue;
                    var loc = tileEntity.ToWorldPos();
                    if (config.range >= 0 && Vector3.Distance(player.position, loc) > config.range)
                        continue;
                    var entity = (tileEntity as TileEntityComposite);
                    if (entity != null)
                    {
                        var lootable = entity.GetFeature<TEFeatureStorage>();
                        if (lootable != null && lootable.ItemGrid.PlayerOwned)
                        {
                            var lockable = entity.GetFeature<ILockable>();
                            if (lockable == null || !lockable.IsLocked() || lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier))
                            {


                                if (!entity.IsUserAccessing())
                                {
                                    storageList.Add(loc);
                                    storageDict.Add(loc, lootable);
                                }
                            }
                        }
                        continue;
                    }

                }
                c.ExitReadLock();

                
            }
            var pos = world.GetPrimaryPlayer().position;
            storageList.Sort(delegate (Vector3i a, Vector3i b) {
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
            Dbgl($"Got {storageList.Count} storages");
            
            Dictionary<int, int> dict = new Dictionary<int, int>();
            var bag = world.GetPrimaryPlayer().bag;
            var slots = bag.ItemGrid.items;
            var toolbelt = world.GetPrimaryPlayer().inventory;
            var tslots = toolbelt.ItemGrid.items;

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
                    if (storageDict[v] is TEFeatureStorage tel)
                    {

                        for (int j = tel.ItemGrid.items.Length - 1; j >= 0; j--)
                        {
                            if (tel.ItemGrid.SlotLocks != null && tel.ItemGrid.SlotLocks.length > j && tel.ItemGrid.SlotLocks[j])
                                continue;

                            var item = tel.ItemGrid.items[j];
                            if (item.IsEmpty())
                                continue;

                            var initItem = tel.ItemGrid.items[j].Clone();
                            int num = tel.ItemGrid.items[j].count;
                            if (tel.ItemGrid.items[j].itemValue.type == slot.itemValue.type && slot.CanStackPartly(ref num))
                            {
                                tel.ItemGrid.items[j].count -= num;
                                if (i < slots.Length)
                                {
                                    slots[idx].count += num;
                                    bag.onBackpackChanged();
                                }
                                else
                                {
                                    tslots[idx].count += num;
                                    toolbelt.CallOnToolbeltChangedInternal();
                                }
                                tel.SetModified();
                                int moved = initItem.count - tel.ItemGrid.items[j].count;
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
