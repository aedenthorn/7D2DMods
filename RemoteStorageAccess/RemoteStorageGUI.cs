using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace RemoteStorageAccess
{
    public class RemoteStorageGUI: MonoBehaviour
    {
        public Vector2 scrollPosition;
        public ModConfig config;
        private void Awake()
        {
            config = Main.config;
        }

        private void OnGUI()
        {

            if (!config.modEnabled || !Main.showingList || GameManager.Instance?.World?.GetPrimaryPlayer() == null)
                return;

            Main.windowRect.height = Mathf.Clamp((Main.sortedStorageList.Count) * (config.buttonHeight + config.betweenSpace) + 40, 50, config.windowHeight);

            GUI.backgroundColor = new Color(config.windowBackgroundColorR, config.windowBackgroundColorG, config.windowBackgroundColorB, config.windowBackgroundColorA);

            Main.windowRect = GUI.Window(424242, Main.windowRect, new GUI.WindowFunction(WindowBuilder), config.windowTitleText);

            if (!Input.GetKey(KeyCode.Mouse0) && (Main.windowRect.x != config.windowPositionX || Main.windowRect.y != config.windowPositionY))
            {
                config.windowPositionX = (int)Main.windowRect.x;
                config.windowPositionY = (int)Main.windowRect.y;
                var path = Path.Combine(AedenthornUtils.GetAssetPath(this, true), "config.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
        }

        private void WindowBuilder(int id)
        {
            GUILayout.BeginVertical();
            GUI.DragWindow(new Rect(0, 0, config.buttonWidth + config.buttonHeight + 30, 20));

            Main.editing = false;
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.Width(config.buttonWidth + config.buttonHeight + 30), GUILayout.Height(Main.windowRect.height - 30) });
            for (int i = 0; i < Main.sortedStorageList.Count; i++)
            {
                GUILayout.BeginHorizontal();
                var coords = Main.ToXYZ(Main.sortedStorageList[i]);
                var naming = Main.currentStorageDict[Main.sortedStorageList[i]].naming;
                if (naming)
                {
                    Main.editing = true;
                    Main.nameDict[coords] = GUILayout.TextField(Main.nameDict[Main.ToXYZ(Main.sortedStorageList[i])], new GUILayoutOption[]{
                        GUILayout.Width(config.buttonWidth),
                        GUILayout.Height(config.buttonHeight)
                    });
                }
                else
                {
                    var color = GUI.backgroundColor;
                    if (Main.sortedStorageList.IndexOf(Main.currentStorage) == i && GameManager.Instance.World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                    {
                        GUI.backgroundColor = new Color(config.currentColorR, config.currentColorG, config.currentColorB, config.currentColorA);
                    }
                    if (GUILayout.Button(Main.nameDict[coords] != "" ? Main.nameDict[coords] : Main.sortedStorageList[i] + "", new GUILayoutOption[]{
                        GUILayout.Width(config.buttonWidth),
                        GUILayout.Height(config.buttonHeight)
                    }))
                    {
                        Main.Dbgl($"Pressed button {i}");

                        File.WriteAllText(Main.nameDictPath, JsonConvert.SerializeObject(Main.nameDict));
                        Main.currentStorage = Main.sortedStorageList[i];
                        Main.OpenStorage();
                    }
                    GUI.backgroundColor = color;
                }
                if (GUILayout.Button(naming ? "x" : "e", new GUILayoutOption[]{
                        GUILayout.Width(config.buttonHeight),
                        GUILayout.Height(config.buttonHeight)
                    }))
                {
                    naming = !naming;
                    Main.currentStorageDict[Main.sortedStorageList[i]].naming = naming;
                    Main.Dbgl($"Pressed edit button {i}, naming {naming}");
                    if (!naming)
                    {
                        File.WriteAllText(Main.nameDictPath, JsonConvert.SerializeObject(Main.nameDict));
                        Main.ReloadStorages();
                    }
                }
                GUILayout.EndHorizontal();
                if (i < Main.sortedStorageList.Count - 1)
                    GUILayout.Space(config.betweenSpace);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

    }
}