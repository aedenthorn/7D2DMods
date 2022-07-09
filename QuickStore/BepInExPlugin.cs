using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace QuickStore
{
    [BepInPlugin("aedenthorn.QuickStore", "QuickStore", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        private static List<Vector3i> storageList = new List<Vector3i>();
        private static Dictionary<Vector3i, TileEntityLootContainer> storageDict = new Dictionary<Vector3i, TileEntityLootContainer>();

        public static void Dbgl(string str = "", LogLevel logLevel = LogLevel.Debug)
        {
            if (isDebug.Value)
                context.Logger.Log(logLevel, str);
        }
        private void Awake()
        {

            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind<bool>("General", "IsDebug", true, "Enable debug logs");
            //nexusID = Config.Bind<int>("General", "NexusID", 88, "Nexus mod ID for updates");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!modEnabled.Value || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;

                if (Input.GetKeyDown(KeyCode.Q) && !___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting") && !___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("backpack"))
                {
                    Dbgl($"Pressed Q");
                    StoreItems(___m_World);
                }
            }
        }
        private static void StoreItems(World world)
        {
            Dbgl($"Storing items");

            storageList.Clear();
            storageDict.Clear();
            for(int i = 0; i < world.ChunkClusters.Count; i++)
            {
                var cc = world.ChunkClusters[i];
                ReaderWriterLockSlim sync = (ReaderWriterLockSlim)AccessTools.Field(typeof(WorldChunkCache), "sync").GetValue(cc);
                sync.EnterReadLock();
                foreach (var c in cc.chunks.dict.Values)
                {
                    var cp = new Vector3i(c.X * 16, c.Y * 256, c.Z * 16);
                    DictionaryList<Vector3i, TileEntity> entities = (DictionaryList<Vector3i, TileEntity>)AccessTools.Field(typeof(Chunk), "tileEntities").GetValue(c);
                    foreach (var kvp in entities.dict)
                    {
                        if (kvp.Value is TileEntityLootContainer && (kvp.Value as TileEntityLootContainer).bPlayerStorage)
                        {
                            var loc = cp + kvp.Value.localChunkPos;
                            storageList.Add(loc);
                            storageDict.Add(loc, kvp.Value as TileEntityLootContainer);
                        }
                    }
                }
                sync.ExitReadLock();
            }
            var pos = world.GetPrimaryPlayer().position;
            storageList.Sort(delegate (Vector3i a, Vector3i b) { 
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
            Dbgl($"Got {storageList.Count} storages");
            int count = 0;
            var items = world.GetPrimaryPlayer().bag.GetSlots();
            for(int i = 0; i < items.Length; i++)
            {
                if (items[i].IsEmpty())
                    continue;
                foreach (var v in storageList)
                {
                    if (Array.Exists(storageDict[v].GetItems(), e => e.itemValue.type == items[i].itemValue.type))
                    {
                        var had = items[i].Clone();
                        if ((storageDict[v].TryStackItem(0, items[i]) && items[i].count == 0) || storageDict[v].AddItem(items[i]))
                        {
                            items[i].Clear();
                            storageDict[v].SetModified();
                            count++;
                        }
                        if (had.count > items[i].count)
                        {
                            had.count -= items[i].count;
                            storageDict[v].SetModified();
                            ((XUiC_CollectedItemList)AccessTools.Field(typeof(XUiM_PlayerInventory), "sideBarItemInfo").GetValue(world.GetPrimaryPlayer().PlayerUI.xui.PlayerInventory)).RemoveItemStack(had);
                        }
                        if (items[i].IsEmpty())
                            break;
                    }
                }
            }
            Dbgl($"Stored {count} items");
        }
    }
}
