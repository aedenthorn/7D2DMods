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
            config = RemoteStorageAccess.config;
        }

        private void OnGUI()
        {

            if (!config.modEnabled || !RemoteStorageAccess.showingList || GameManager.Instance?.World?.GetPrimaryPlayer() == null)
                return;

            RemoteStorageAccess.windowRect.height = Mathf.Clamp((RemoteStorageAccess.sortedStorageList.Count) * (config.buttonHeight + config.betweenSpace) + 40, 50, config.windowHeight);

            GUI.backgroundColor = new Color(config.windowBackgroundColorR, config.windowBackgroundColorG, config.windowBackgroundColorB, config.windowBackgroundColorA);

            RemoteStorageAccess.windowRect = GUI.Window(424242, RemoteStorageAccess.windowRect, new GUI.WindowFunction(WindowBuilder), config.windowTitleText);

            if (!Input.GetKey(KeyCode.Mouse0) && (RemoteStorageAccess.windowRect.x != config.windowPositionX || RemoteStorageAccess.windowRect.y != config.windowPositionY))
            {
                config.windowPositionX = (int)RemoteStorageAccess.windowRect.x;
                config.windowPositionY = (int)RemoteStorageAccess.windowRect.y;
                var path = Path.Combine(AedenthornUtils.GetAssetPath(this, true), "config.json");
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
        }

        private void WindowBuilder(int id)
        {
            GUILayout.BeginVertical();
            GUI.DragWindow(new Rect(0, 0, config.buttonWidth + config.buttonHeight + 30, 20));

            RemoteStorageAccess.editing = false;
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.Width(config.buttonWidth + config.buttonHeight + 30), GUILayout.Height(RemoteStorageAccess.windowRect.height - 30) });
            for (int i = 0; i < RemoteStorageAccess.sortedStorageList.Count; i++)
            {
                GUILayout.BeginHorizontal();
                var coords = RemoteStorageAccess.ToXYZ(RemoteStorageAccess.sortedStorageList[i]);
                var naming = RemoteStorageAccess.currentStorageDict[RemoteStorageAccess.sortedStorageList[i]].naming;
                if (naming)
                {
                    RemoteStorageAccess.editing = true;
                    RemoteStorageAccess.nameDict[coords] = GUILayout.TextField(RemoteStorageAccess.nameDict[RemoteStorageAccess.ToXYZ(RemoteStorageAccess.sortedStorageList[i])], new GUILayoutOption[]{
                        GUILayout.Width(config.buttonWidth),
                        GUILayout.Height(config.buttonHeight)
                    });
                }
                else
                {
                    var color = GUI.backgroundColor;
                    if (RemoteStorageAccess.sortedStorageList.IndexOf(RemoteStorageAccess.currentStorage) == i && GameManager.Instance.World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("looting"))
                    {
                        GUI.backgroundColor = new Color(config.currentColorR, config.currentColorG, config.currentColorB, config.currentColorA);
                    }
                    if (GUILayout.Button(RemoteStorageAccess.nameDict[coords] != "" ? RemoteStorageAccess.nameDict[coords] : RemoteStorageAccess.sortedStorageList[i] + "", new GUILayoutOption[]{
                        GUILayout.Width(config.buttonWidth),
                        GUILayout.Height(config.buttonHeight)
                    }))
                    {
                        RemoteStorageAccess.Dbgl($"Pressed button {i}");
                        RemoteStorageAccess.currentVehicleStorage = -1;
                        File.WriteAllText(RemoteStorageAccess.nameDictPath, JsonConvert.SerializeObject(RemoteStorageAccess.nameDict));
                        RemoteStorageAccess.currentStorage = RemoteStorageAccess.sortedStorageList[i];
                        RemoteStorageAccess.OpenStorage();
                    }
                    GUI.backgroundColor = color;
                }
                if (GUILayout.Button(naming ? "x" : "e", new GUILayoutOption[]{
                        GUILayout.Width(config.buttonHeight),
                        GUILayout.Height(config.buttonHeight)
                    }))
                {
                    naming = !naming;
                    RemoteStorageAccess.currentStorageDict[RemoteStorageAccess.sortedStorageList[i]].naming = naming;
                    RemoteStorageAccess.Dbgl($"Pressed edit button {i}, naming {naming}");
                    if (!naming)
                    {
                        File.WriteAllText(RemoteStorageAccess.nameDictPath, JsonConvert.SerializeObject(RemoteStorageAccess.nameDict));
                        RemoteStorageAccess.ReloadStorages();
                    }
                }
                GUILayout.EndHorizontal();
                if (i < RemoteStorageAccess.sortedStorageList.Count - 1)
                    GUILayout.Space(config.betweenSpace);
            }
            if(RemoteStorageAccess.sortedStorageList.Count > 0 && RemoteStorageAccess.vehicleList.Count > 0)
                    GUILayout.Space(config.betweenSpace);
            for (int i = 0; i < RemoteStorageAccess.vehicleList.Count; i++)
            {
                GUILayout.BeginHorizontal();
                var color = GUI.backgroundColor;
                if (RemoteStorageAccess.currentVehicleStorage == i && GameManager.Instance.World.GetPrimaryPlayer().PlayerUI.windowManager.IsWindowOpen("vehicleStorage"))
                {
                    GUI.backgroundColor = new Color(config.currentColorR, config.currentColorG, config.currentColorB, config.currentColorA);
                }
                if (GUILayout.Button(Localization.Get(EntityClass.list[RemoteStorageAccess.vehicleList[i].entityClass].entityClassName), new GUILayoutOption[]{
                        GUILayout.Width(config.buttonWidth),
                        GUILayout.Height(config.buttonHeight)
                    }))
                {
                    RemoteStorageAccess.Dbgl($"Pressed vehicle button {i}");
                    File.WriteAllText(RemoteStorageAccess.nameDictPath, JsonConvert.SerializeObject(RemoteStorageAccess.nameDict));
                    RemoteStorageAccess.currentVehicleStorage = i;
                    RemoteStorageAccess.OpenVehicleStorage();
                }
                GUI.backgroundColor = color;
                GUILayout.EndHorizontal();
                if (i < RemoteStorageAccess.vehicleList.Count - 1)
                    GUILayout.Space(config.betweenSpace);

            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

    }
}