using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using UnityEngine;
using WorldGenerationEngineFinal;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace RemoteStorageAccess
{
    public class Main : IModApi
    {


        public static List<Vector3i> sortedStorageList = new List<Vector3i>();
        public static Dictionary<Vector3i, StorageData> storageDict = new Dictionary<Vector3i, StorageData>();
        public static Vector3i currentStorage;
        public static Dictionary<string, string> nameDict = new Dictionary<string, string>();
        public static bool showingList;
        public static string nameDictPath;
        public static float elapsedTime;
        public static bool editing;
        public static Rect windowRect;

        public static ModConfig config;
        public static Main context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            LoadConfig();

            windowRect = new Rect(config.windowPositionX, config.windowPositionY, config.buttonWidth + config.buttonHeight + 40, config.windowHeight);


            GameObject go = new GameObject("RemoteStorageGUI");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<RemoteStorageGUI>();

        }

        public void LoadConfig()
        {
            var path = Path.Combine(AedenthornUtils.GetAssetPath(this, true), "config.json");
            if (!File.Exists(path))
            {
                config = new ModConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            else
            {
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
            }
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            Debug.Log((prefix ? mod.ModInfo.Name.Value : "") + str);
        }

        [HarmonyPatch(typeof(GameManager), "StartGame")]
        static class GameManager_StartGame_Patch
        {
            static void Prefix()
            {
                nameDictPath = null;
            }
        }
        [HarmonyPatch(typeof(GameManager), "lootContainerOpened")]
        static class GameManager_lootContainerOpened_Patch
        {
            static void Postfix(TileEntityLootContainer _te)
            {
                if (!config.modEnabled || !_te.bPlayerStorage)
                    return;
                if (sortedStorageList.Count == 0)
                    ReloadStorages();
                if (sortedStorageList.Contains(_te.ToWorldPos()) && currentStorage != _te.ToWorldPos())
                {
                    currentStorage = _te.ToWorldPos();
                }
            }
        }
        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;

                if (nameDictPath is null)
                {
                    nameDictPath = Path.Combine(AedenthornUtils.GetAssetPath(context, true), GameManager.Instance.World.Guid + ".json");
                    ReloadStorages();
                }

                if (showingList && !editing)
                {
                    elapsedTime += Time.deltaTime;
                    if (elapsedTime > config.pollInterval)
                    {
                        elapsedTime = 0;
                        ReloadStorages();
                        return;
                    }
                }

                if (AedenthornUtils.CheckKeyDown(config.openCurrentKey))
                {
                    Dbgl($"Pressed open key");
                    if (___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                    {
                        ___m_World.GetPrimaryPlayer().PlayerUI.windowManager.CloseAllOpenWindows(null, true);
                    }
                    else
                    {
                        ReloadStorages();
                        if (sortedStorageList.Count == 0)
                            return;
                        currentStorage = sortedStorageList[0];
                        OpenStorage();
                    }
                }
                else if (AedenthornUtils.CheckKeyDown(config.openWindowKey))
                {
                    Dbgl($"Pressed window key");
                    if (showingList)
                    {
                        Dbgl($"Closing window");
                        File.WriteAllText(nameDictPath, JsonConvert.SerializeObject(nameDict));
                        showingList = false;
                    }
                    else
                    {
                        ReloadStorages();
                        if (sortedStorageList.Count == 0)
                            return;
                        Dbgl($"Opening window");
                        showingList = true;
                    }
                }
                else if (___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                {
                    if (AedenthornUtils.CheckKeyDown(config.openNextKey))
                    {
                        Dbgl($"Pressed next key");
                        ReloadStorages();
                        if (sortedStorageList.Count == 0)
                            return;
                        int i = sortedStorageList.IndexOf(currentStorage);
                        if (i < 0 || i >= sortedStorageList.Count - 1)
                            currentStorage = sortedStorageList[0];
                        else
                            currentStorage = sortedStorageList[i + 1];
                        OpenStorage();
                        
                    }
                    else if (AedenthornUtils.CheckKeyDown(config.openPrevKey))
                    {
                        Dbgl($"Pressed prev key");
                        ReloadStorages();
                        if (sortedStorageList.Count == 0)
                            return;
                        int i = sortedStorageList.IndexOf(currentStorage);
                        if (i < 0)
                            currentStorage = sortedStorageList[0];
                        else if (i == 0)
                            currentStorage = sortedStorageList[sortedStorageList.Count - 1];
                        else
                            currentStorage = sortedStorageList[i - 1];
                        OpenStorage();

                    }
                }
            }

        }
        [HarmonyPatch(typeof(XUiC_LootWindowGroup), nameof(XUiC_LootWindowGroup.SetTileEntityChest))]
        static class XUiC_LootWindowGroup_SetTileEntityChest_Patch
        {
            static void Postfix(XUiC_LootWindow ___lootWindow, TileEntityLootContainer _te, ref string ___lootingHeader)
            {
                if (!config.modEnabled || !_te.bPlayerStorage || GameManager.Instance?.World is null)
                    return;
                if(sortedStorageList.Count == 0)
                    ReloadStorages();
                ___lootingHeader = config.uiTitleText;
                if (nameDict.TryGetValue(ToXYZ(_te.ToWorldPos()), out string name) && name != "")
                {
                    AccessTools.Field(typeof(XUiC_LootWindow), "lootContainerName").SetValue(___lootWindow, name);
                    ___lootWindow.RefreshBindings();
                }
            }
        }
        
        [HarmonyPatch(typeof(BlockSecureLoot), nameof(BlockSecureLoot.GetActivationText))]
        static class BlockSecureLoot_GetActivationText_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling BlockSecureLoot_GetActivationText");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Block), nameof(Block.GetLocalizedBlockName)))
                    {
                        Dbgl($"Using method to get string for storage hover name at {i}");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Main), nameof(Main.GetStorageNameForTranspiler))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_0));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        
        [HarmonyPatch(typeof(BlockSecureLootSigned), nameof(BlockSecureLootSigned.GetActivationText))]
        static class BlockSecureLootSigned_GetActivationText_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling BlockSecureLootSigned_GetActivationText");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Block), nameof(Block.GetLocalizedBlockName)))
                    {
                        Dbgl($"Using method to get string for storage hover name at {i}");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Main), nameof(Main.GetStorageNameForTranspiler))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_0));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static string GetStorageNameForTranspiler(string input, TileEntityLootContainer te)
        {
            if (!config.modEnabled || !nameDict.TryGetValue(ToXYZ(te.ToWorldPos()), out string name) || name == "")
                return input;
            return name;
        }

        public static void OpenStorage()
        {
            if (sortedStorageList.Count > 0)
            {
                GameManager.Instance.World.GetPrimaryPlayer().PlayerUI.windowManager.CloseAllOpenWindows(null, true);
                GameManager.Instance.TELockServer(storageDict[currentStorage].cluster, currentStorage, -1, GameManager.Instance.World.GetPrimaryPlayer().entityId, null);
            }
        }
        public static void ReloadStorages()
        {
            var world = GameManager.Instance?.World;
            if (world == null)
                return;
            Dbgl($"Loading storages, {world.ChunkClusters.Count} ccs");

            sortedStorageList.Clear();
            storageDict.Clear();
            nameDict = File.Exists(nameDictPath) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(nameDictPath)) : new Dictionary<string, string>();
            for(int i = 0; i < world.ChunkClusters.Count; i++)
            {
                var cc = world.ChunkClusters[i];
                ReaderWriterLockSlim sync = (ReaderWriterLockSlim)AccessTools.Field(typeof(WorldChunkCache), "sync").GetValue(cc);
                sync.EnterReadLock();
                foreach (var c in cc.chunks.dict.Values)
                {
                    DictionaryList<Vector3i, TileEntity> entities = (DictionaryList<Vector3i, TileEntity>)AccessTools.Field(typeof(Chunk), "tileEntities").GetValue(c);
                    foreach (var kvp in entities.dict)
                    {
                        if (kvp.Value is TileEntityLootContainer && (kvp.Value as TileEntityLootContainer).bPlayerStorage)
                        {
                            (kvp.Value as TileEntityLootContainer).bWasTouched = (kvp.Value as TileEntityLootContainer).bTouched;
                            var loc = kvp.Value.ToWorldPos();
                            sortedStorageList.Add(loc);
                            storageDict.Add(loc, new StorageData() { chunk = c, cluster = i, te = kvp.Value as TileEntityLootContainer });
                            if(!nameDict.ContainsKey(ToXYZ(loc)))
                                nameDict.Add(ToXYZ(loc), "");
                        }
                    }
                }
                sync.ExitReadLock();
            }
            Dbgl($"Got {sortedStorageList.Count} storages");
            var pos = world.GetPrimaryPlayer().position;
            sortedStorageList.Sort(delegate (Vector3i a, Vector3i b) { 
                if(nameDict[ToXYZ(a)] != "" || nameDict[ToXYZ(b)] != "")
                {
                    return nameDict[ToXYZ(a)].CompareTo(nameDict[ToXYZ(b)]);
                }
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
        }

        public static string ToXYZ(Vector3i v)
        {
            return $"{v.x},{v.y},{v.z}";
        }
    }
}
