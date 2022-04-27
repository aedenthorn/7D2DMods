using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using WorldGenerationEngineFinal;

namespace StorageAnywhere
{
    [BepInPlugin("aedenthorn.StorageAnywhere", "StorageAnywhere", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<Vector2> updatesPosition;
        public static ConfigEntry<int> updateTextWidth;
        public static ConfigEntry<int> fontSize;
        public static ConfigEntry<Color> updateFontColor;
        public static ConfigEntry<int> buttonWidth;
        public static ConfigEntry<int> buttonHeight;
        public static ConfigEntry<int> betweenSpace;
        public static ConfigEntry<float> windowHeight;
        public static ConfigEntry<Color> windowBackgroundColor;

        public static ConfigEntry<string> windowTitleText;

        private static Vector2 scrollPosition;
        public static float rowWidth;
        private static Rect windowRect;

        private static List<Vector3i> storageList = new List<Vector3i>();
        private static Dictionary<Vector3i, int> clusterDict = new Dictionary<Vector3i, int>();
        private static int currentIndex = 0;
        private static Dictionary<Vector3i, Chunk> chunkDict = new Dictionary<Vector3i, Chunk>();
        private static bool showingList;

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

            updatesPosition = Config.Bind<Vector2>("UI", "UpdatesPosition", new Vector2(40, 40), "Position of the updates list on the screen");
            updateTextWidth = Config.Bind<int>("UI", "UpdateTextWidth", Screen.width / 6, "Width of the update text (will wrap if it is too long)");
            buttonWidth = Config.Bind<int>("UI", "ButtonWidth", 100, "Width of the update button");
            buttonHeight = Config.Bind<int>("UI", "ButtonHeight", 30, "Height of the update button");
            betweenSpace = Config.Bind<int>("UI", "BetweenSpace", 10, "Vertical space between each update in list");
            windowHeight = Config.Bind<float>("UI", "WindowHeight", Screen.height / 3, "Height of the update window");
            windowBackgroundColor = Config.Bind<Color>("UI", "WindowBackgroundColor", new Color(1, 1, 1, 0.25f), "Color of the window background");
            windowTitleText = Config.Bind<string>("Text", "WindowTitleText", "<b>Nexus Updates</b>", "Window title when not checking for updates");
            fontSize = Config.Bind<int>("Text", "FontSize", 14, "Size of the text in the updates list");

            ApplyConfig();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
        }
        private static void ApplyConfig()
        {
            rowWidth = updateTextWidth.Value + buttonWidth.Value;

            windowRect = new Rect(updatesPosition.Value.x, updatesPosition.Value.y, rowWidth + 50, windowHeight.Value);

        }
        private void OnGUI()
        {
            if (modEnabled.Value && showingList)
            {
                GUI.backgroundColor = windowBackgroundColor.Value;

                windowRect = GUI.Window(424242, windowRect, new GUI.WindowFunction(WindowBuilder), windowTitleText.Value);

                if (!Input.GetKey(KeyCode.Mouse0) && (windowRect.x != updatesPosition.Value.x || windowRect.y != updatesPosition.Value.y))
                {
                    updatesPosition.Value = new Vector2(windowRect.x, windowRect.y);
                    Config.Save();
                }
            }
        }

        private void WindowBuilder(int id)
        {
            GUILayout.BeginVertical();
            GUI.DragWindow(new Rect(0, 0, rowWidth + 50, 20));

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.Width(rowWidth + 40), GUILayout.Height(windowHeight.Value - 30) });
            for(int i = 0; i < storageList.Count; i++)
            {


                if (GUILayout.Button(storageList[i]+ "", new GUILayoutOption[]{
                        GUILayout.Width(buttonWidth.Value),
                        GUILayout.Height(buttonHeight.Value)
                    }))
                {
                    OpenStorage(GameManager.Instance, GameManager.Instance.World);
                }
                GUILayout.Space(betweenSpace.Value);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }


        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!modEnabled.Value || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;

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
                        currentIndex = 0;
                        OpenStorage(__instance, ___m_World);
                    }
                }
                else if (___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                {
                    if (Input.GetKeyDown(KeyCode.RightArrow))
                    {
                        Dbgl($"Pressed right");
                        ReloadStorages(___m_World);
                        currentIndex++;
                        currentIndex %= clusterDict.Count;
                        OpenStorage(__instance, ___m_World);

                    }
                    else if (Input.GetKeyDown(KeyCode.LeftArrow))
                    {
                        Dbgl($"Pressed left");
                        ReloadStorages(___m_World);
                        currentIndex--;
                        if (currentIndex < 0)
                            currentIndex = clusterDict.Count - 1;
                        OpenStorage(__instance, ___m_World);

                    }
                }
                else if (Input.GetKeyDown(KeyCode.RightBracket))
                {
                    if (showingList)
                    {
                        Dbgl($"Closing window");

                        showingList = false;
                    }
                    else
                    {
                        Dbgl($"Opening window at {windowRect}");
                        ReloadStorages(___m_World);
                        showingList = true;
                    }
                }
            }

        }
        private static void OpenStorage(GameManager manager, World world)
        {
            if (clusterDict.Count > 0)
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
                            var loc = cp + kvp.Value.localChunkPos;
                            storageList.Add(loc);
                            clusterDict.Add(loc, i);
                            //chunkDict.Add(loc, c);
                        }
                    }
                }
                sync.ExitReadLock();
            }
            var pos = world.GetPrimaryPlayer().position;
            storageList.Sort(delegate (Vector3i a, Vector3i b) { return Vector3.Distance(a, pos).CompareTo(Vector3.Distance(b, pos)); });
            Dbgl($"Got {storageList.Count} storages");
        }
    }
}
