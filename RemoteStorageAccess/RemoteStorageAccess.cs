using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace RemoteStorageAccess
{
    public class RemoteStorageAccess : IModApi
    {


        public static List<Vector3i> sortedStorageList = new List<Vector3i>();
        public static Dictionary<Vector3i, StorageData> currentStorageDict = new Dictionary<Vector3i, StorageData>();
        public static Dictionary<Vector3i, StorageData> knownStorageDict = new Dictionary<Vector3i, StorageData>();
        public static Vector3i currentStorage;
        public static Dictionary<string, string> nameDict = new Dictionary<string, string>();
        public static bool showingList;
        public static string nameDictPath;
        public static float elapsedTime;
        public static bool editing;
        public static Rect windowRect;

        public static ModConfig config;
        public static RemoteStorageAccess context;
        public static Mod mod;
        public static bool openingStorage;
        public static bool openingStorage2;

        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            windowRect = new Rect(config.windowPositionX, config.windowPositionY, config.buttonWidth + config.buttonHeight + 40, config.windowHeight);
            GameObject go = new GameObject("RemoteStorageGUI");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<RemoteStorageGUI>();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        public void LoadConfig()
        {
            var path = Path.Combine(AedenthornUtils.GetAssetPath(this, true), "config.json");
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
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

        [HarmonyPatch(typeof(GameManager), "StartGame")]
        static class GameManager_StartGame_Patch
        {
            static void Prefix()
            {
                nameDictPath = null;
                knownStorageDict.Clear();

            }
        }
        [HarmonyPatch(typeof(GameManager), "lootContainerOpened")]
        static class GameManager_lootContainerOpened_Patch
        {
            static void Postfix(ITileEntity _te)
            {
                ITileEntityLootable selfOrFeature = _te.GetSelfOrFeature<ITileEntityLootable>();
                if (selfOrFeature == null || !config.modEnabled || !selfOrFeature.bPlayerStorage)
                {
                    return;
                }

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

            static void Postfix(World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World?.GetPrimaryPlayer() == null)
                    return;
                if (nameDictPath is null)
                {
                    nameDictPath = Path.Combine(AedenthornUtils.GetAssetPath(context, true), ___m_World.Guid + ".json");
                    ReloadStorages();
                }

                if (showingList && !editing)
                {
                    elapsedTime += Time.deltaTime;
                    if (elapsedTime > config.pollInterval)
                    {
                        elapsedTime = 0;
                        ReloadStorages(true);
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
                        int i = sortedStorageList.IndexOf(currentStorage);
                        if (i < 0 || i >= sortedStorageList.Count)
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

        [HarmonyPatch(typeof(XUiC_LootWindow), nameof(XUiC_LootWindow.Update))]
        static class XUiC_LootWindow_Update_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiC_LootWindow_Update");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldsfld && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(Constants), nameof(Constants.cCollectItemDistance)))
                    {
                        Dbgl($"Overriding max distance to keep loot window open");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RemoteStorageAccess), nameof(RemoteStorageAccess.GetCollectDistanceForTranspiler))));
                        break;
                    }
                }

                return codes.AsEnumerable();
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
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RemoteStorageAccess), nameof(RemoteStorageAccess.GetStorageNameForTranspiler))));
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
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RemoteStorageAccess), nameof(RemoteStorageAccess.GetStorageNameForTranspiler))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_0));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        private static float GetCollectDistanceForTranspiler(float distance)
        {
            if (!config.modEnabled)
                return distance;
            return float.MaxValue / 2f;
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
                openingStorage = true;
                openingStorage2 = true;
                GameManager.Instance.World.GetPrimaryPlayer().PlayerUI.windowManager.CloseAllOpenWindows(null, true);
                GameManager.Instance.TELockServer(currentStorageDict[currentStorage].cluster, currentStorage, currentStorageDict[currentStorage].te.Parent.EntityId, GameManager.Instance.World.GetPrimaryPlayer().entityId, "container");

            }
        }
        public static void ReloadStorages(bool polling = false)
        {
            var world = GameManager.Instance?.World;
            if (world == null)
                return;
            if(!polling)
                Dbgl($"Loading storages, {world.ChunkClusters.Count} ccs");

            var pos = world.GetPrimaryPlayer().position;

            currentStorageDict.Clear();
            nameDict = File.Exists(nameDictPath) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(nameDictPath)) : new Dictionary<string, string>();
            for(int i = 0; i < world.ChunkClusters.Count; i++)
            {

                var cc = world.ChunkClusters[i];

                if (!polling)
                    Dbgl($"cc has {cc.chunks.dict.Count} chunks");
                foreach (var key in cc.chunks.dict.Keys.ToArray())
                {
                    var c= cc.chunks.dict[key];
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
                                if(lockable == null || !lockable.IsLocked() || lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier))
                                {
                                    lootable.bWasTouched = lootable.bTouched;
                                    knownStorageDict[loc] = new StorageData() { chunk = c, cluster = i, te = lootable };
                                    if (config.range <= 0 || Vector3.Distance(pos, loc) < config.range)
                                        currentStorageDict[loc] = new StorageData() { chunk = c, cluster = i, te = lootable };
                                    var xyz = ToXYZ(loc);
                                    if (!nameDict.ContainsKey(xyz))
                                    {
                                        nameDict[xyz] = "";
                                    }
                                    if (nameDict[xyz] == "")
                                    {
                                        var text = entity.GetFeature<ITileEntitySignable>() as TEFeatureSignable;
                                        if (text != null && !string.IsNullOrEmpty(text.GetAuthoredText().Text))
                                        {
                                            nameDict[xyz] = text.GetAuthoredText().Text;
                                        }

                                    }
                                }
                            }
                        }
                    }
                    c.ExitReadLock();

                }
            }
            if (!polling)
                Dbgl($"Got {currentStorageDict.Count} storages");
            sortedStorageList = new List<Vector3i>(currentStorageDict.Keys.ToArray());
            sortedStorageList.Sort(delegate (Vector3i a, Vector3i b) { 
                if(nameDict[ToXYZ(a)] != "" && nameDict[ToXYZ(b)] != "")
                {
                    return nameDict[ToXYZ(a)].CompareTo(nameDict[ToXYZ(b)]);
                }
                else if(nameDict[ToXYZ(a)] != "")
                {
                    return -1;
                }
                else if(nameDict[ToXYZ(b)] != "")
                {
                    return 1;
                }
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
        }

        [HarmonyPatch(typeof(FlexibleCursor), nameof(FlexibleCursor.SetNavigationTarget))]
        static class FlexibleCursor_SetNavigationTarget_Patch
        {
            static bool Prefix()
            {
                return false;

                if (openingStorage)
                {
                    openingStorage = false;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(FlexibleCursor), nameof(FlexibleCursor.SetNavigationTargetLater))]
        static class FlexibleCursor_SetNavigationTargetLater_Patch
        {
            static bool Prefix()
            {
                return false;

                if (openingStorage2)
                {
                    openingStorage2 = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SoftCursor), nameof(SoftCursor.SetNavigationTarget))]
        static class SoftCursor_SetNavigationTarget_Patch
        {
            static bool Prefix()
            {
                return false;

                if (openingStorage)
                {
                    openingStorage = false;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(SoftCursor), nameof(SoftCursor.SetNavigationTargetLater))]
        static class SoftCursor_SetNavigationTargetLater_Patch
        {
            static bool Prefix()
            {
                return false;

                if (openingStorage2)
                {
                    openingStorage2 = false;
                    return false;
                }
                return true;
            }
        }

        public static string ToXYZ(Vector3i v)
        {
            return $"{v.x},{v.y},{v.z}";
        }
    }
}
