using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using WorldGenerationEngineFinal;
using Path = System.IO.Path;

namespace RemoteStorageAccess
{
    [BepInPlugin("aedenthorn.RemoteStorageAccess", "RemoteStorageAccess", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<Vector2> windowPosition;
        public static ConfigEntry<int> fontSize;
        public static ConfigEntry<Color> updateFontColor;
        public static ConfigEntry<int> buttonWidth;
        public static ConfigEntry<int> buttonHeight;
        public static ConfigEntry<int> betweenSpace;
        public static ConfigEntry<float> windowHeight;
        public static ConfigEntry<float> pollInterval;
        public static ConfigEntry<Color> windowBackgroundColor;

        public static ConfigEntry<string> windowTitleText;

        private static Vector2 scrollPosition;
        public static float rowWidth;
        private static Rect windowRect;

        private static List<Vector3i> storageList = new List<Vector3i>();
        private static Dictionary<Vector3i, int> clusterDict = new Dictionary<Vector3i, int>();
        private static int currentIndex = 0;
        private static Dictionary<Vector3i, Chunk> chunkDict = new Dictionary<Vector3i, Chunk>();
        private static Dictionary<string, string> nameDict = new Dictionary<string, string>();
        private static bool showingList;
        private static string jsonPath;
        private static float elapsedTime;

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
            pollInterval = Config.Bind<float>("Options", "PollInterval", 3, "Interval to check for storage changes while window is open.");

            windowPosition = Config.Bind<Vector2>("UI", "WindowPosition", new Vector2(40, 40), "Position of the storage list on the screen");
            buttonWidth = Config.Bind<int>("UI", "ButtonWidth", 100, "Width of storage buttons");
            buttonHeight = Config.Bind<int>("UI", "ButtonHeight", 30, "Height of storage buttons");
            betweenSpace = Config.Bind<int>("UI", "BetweenSpace", 10, "Vertical space between each storage in list");
            windowHeight = Config.Bind<float>("UI", "WindowHeight", Screen.height / 3, "Height of the storage window");
            windowBackgroundColor = Config.Bind<Color>("UI", "WindowBackgroundColor", new Color(1, 1, 1, 0.25f), "Color of the window background");
            windowTitleText = Config.Bind<string>("Text", "WindowTitleText", "<b>Nearby Storage</b>", "Window title");
            fontSize = Config.Bind<int>("Text", "FontSize", 14, "Size of the text in the storage list");

            ApplyConfig();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
        }
        private static void ApplyConfig()
        {
            rowWidth = buttonWidth.Value * 2;

            windowRect = new Rect(windowPosition.Value.x, windowPosition.Value.y, rowWidth + 50, windowHeight.Value);

        }
        private void Update()
        {
            if (!modEnabled.Value || !showingList || GameManager.Instance?.World?.GetPrimaryPlayer() == null)
                return;
            elapsedTime += Time.deltaTime;
            if(elapsedTime > pollInterval.Value)
            {
                elapsedTime = 0;
                ReloadStorages(GameManager.Instance.World);
            }
        }
        private void OnGUI()
        {
            if (!modEnabled.Value || !showingList || GameManager.Instance?.World?.GetPrimaryPlayer() == null)
                return;

            GUI.backgroundColor = windowBackgroundColor.Value;

            windowRect = GUI.Window(424242, windowRect, new GUI.WindowFunction(WindowBuilder), windowTitleText.Value);

            if (!Input.GetKey(KeyCode.Mouse0) && (windowRect.x != windowPosition.Value.x || windowRect.y != windowPosition.Value.y))
            {
                windowPosition.Value = new Vector2(windowRect.x, windowRect.y);
                Config.Save();
            }
        }

        private void WindowBuilder(int id)
        {
            GUILayout.BeginVertical();
            GUI.DragWindow(new Rect(0, 0, rowWidth + 50, 20));

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.Width(rowWidth + 40), GUILayout.Height(windowHeight.Value - 30) });
            for(int i = 0; i < storageList.Count; i++)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(storageList[i]+ "", new GUILayoutOption[]{
                        GUILayout.Width(buttonWidth.Value),
                        GUILayout.Height(buttonHeight.Value)
                    }))
                {
                    Dbgl($"Pressed button {i}");

                    File.WriteAllText(jsonPath, JsonConvert.SerializeObject(nameDict));
                    currentIndex = i;
                    OpenStorage(GameManager.Instance, GameManager.Instance.World);
                }
                nameDict[ToXYZ(storageList[i])] = GUILayout.TextField(nameDict[ToXYZ(storageList[i])], new GUILayoutOption[]{
                        GUILayout.Width(buttonWidth.Value),
                        GUILayout.Height(buttonHeight.Value)
                    });
                GUILayout.EndHorizontal();
                GUILayout.Space(betweenSpace.Value);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }


        [HarmonyPatch(typeof(GameManager), "StartGame")]
        static class GameManager_StartGame_Patch
        {
            static void Prefix()
            {
                jsonPath = null;
            }
        }
        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!modEnabled.Value || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;

                if(jsonPath is null)
                    jsonPath = Path.Combine(AedenthornUtils.GetAssetPath(context, true), GameManager.Instance.World.Guid + ".json");

                if (Input.GetKeyDown(KeyCode.LeftBracket))
                {
                    Dbgl($"Pressed [");
                    if (___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                    {
                        ___m_World.GetPrimaryPlayer().PlayerUI.windowManager.CloseAllOpenWindows(null, true);
                    }
                    else
                    {
                        ReloadStorages(___m_World);
                        if (storageList.Count == 0)
                            return;
                        currentIndex = 0;
                        OpenStorage(__instance, ___m_World);
                    }
                }
                else if (Input.GetKeyDown(KeyCode.RightBracket))
                {
                    Dbgl($"Pressed ]");
                    if (showingList)
                    {
                        Dbgl($"Closing window");
                        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(nameDict));
                        showingList = false;
                    }
                    else
                    {
                        ReloadStorages(___m_World);
                        if (storageList.Count == 0)
                            return;
                        Dbgl($"Opening window");
                        showingList = true;
                    }
                }
                else if (___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                {
                    if (Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        Dbgl($"Pressed right");
                        ReloadStorages(___m_World);
                        if (storageList.Count == 0)
                            return;
                        currentIndex++;
                        currentIndex %= storageList.Count;
                        OpenStorage(__instance, ___m_World);
                        
                    }
                    else if (Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        Dbgl($"Pressed left");
                        ReloadStorages(___m_World);
                        if (storageList.Count == 0)
                            return;
                        currentIndex--;
                        if (currentIndex < 0)
                            currentIndex = storageList.Count - 1;
                        OpenStorage(__instance, ___m_World);

                    }
                }
            }

        }
        private static void OpenStorage(GameManager manager, World world)
        {
            if (currentIndex < storageList.Count)
            {
                world.GetPrimaryPlayer().PlayerUI.windowManager.CloseAllOpenWindows(null, true);
                //AccessTools.Method(typeof(GameManager), "OpenTileEntityUi").Invoke(manager, new object[] { world.GetPrimaryPlayer().entityId, storageList[currentIndex], null });
                manager.TELockServer(clusterDict[storageList[currentIndex]], storageList[currentIndex], -1, world.GetPrimaryPlayer().entityId, null);
            }
        }
        private static void ReloadStorages(World world)
        {
            Dbgl($"Loading storages, {world.ChunkClusters.Count} ccs");

            storageList.Clear();
            clusterDict.Clear();
            nameDict = File.Exists(jsonPath) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(jsonPath)) : new Dictionary<string, string>();
            //chunkDict.Clear();
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
                            Dbgl($"Got storage");
                            var loc = cp + kvp.Value.localChunkPos;
                            storageList.Add(loc);
                            clusterDict.Add(loc, i);
                            if(!nameDict.ContainsKey(ToXYZ(loc)))
                                nameDict.Add(ToXYZ(loc), "");
                            //chunkDict.Add(loc, c);
                        }
                    }
                }
                sync.ExitReadLock();
            }
            Dbgl($"Got {storageList.Count} storages");
            var pos = world.GetPrimaryPlayer().position;
            storageList.Sort(delegate (Vector3i a, Vector3i b) { 
                if(nameDict[ToXYZ(a)] != "" || nameDict[ToXYZ(b)] != "")
                {
                    return nameDict[ToXYZ(a)].CompareTo(nameDict[ToXYZ(b)]);
                }
                return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos));
            });
        }

        private static string ToXYZ(Vector3i v)
        {
            return $"{v.x},{v.y},{v.z}";
        }
    }
}
