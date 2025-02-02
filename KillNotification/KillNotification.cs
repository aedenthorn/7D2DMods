using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UniLinq;
using UnityEngine;
using UnityEngine.UIElements;
using Path = System.IO.Path;

namespace KillNotification
{
    public class KillNotification : IModApi
    {

        public static ModConfig config;
        public static KillNotification context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.AwardKill))]
        static class EntityAlive_AwardKill_Patch
        {
            static void Postfix(EntityAlive __instance, EntityAlive killer)
            {
                if (!config.modEnabled || killer != GameManager.Instance.World.GetPrimaryPlayer())
                    return;
                if (!string.IsNullOrEmpty(config.notificationSound))
                {
                    Manager.PlayInsidePlayerHead(config.notificationSound, -1, 0f, false, false);
                }
                AddIconNotification(Localization.Get(__instance.EntityName, false));

            }
        }
        public static void AddIconNotification(string killed)
        {
            var collectedItemList = GameManager.Instance.World.GetPrimaryPlayer().PlayerUI.xui.CollectedItemList;
            if (collectedItemList.items == null)
            {
                return;
            }
            Transform transform;
            UILabel uilabel;
            for (int i = collectedItemList.items.Count - 1; i >= 0; i--)
            {
                if (collectedItemList.items[i].uiAtlasIcon != string.Empty && collectedItemList.items[i].uiAtlasIcon.EqualsCaseInsensitive(config.notificationIcon))
                {
                    transform = collectedItemList.items[i].Item.transform;
                    if (transform != null)
                    {
                        uilabel = transform.GetComponentInChildren<UILabel>();
                        if (uilabel != null && uilabel.text.Contains(killed))
                        {
                            collectedItemList.items[i].count++;
                            uilabel.text = string.Format(config.notificationText, collectedItemList.items[i].count, killed);
                            collectedItemList.items[i].TimeAdded = Time.time;
                        }
                    }
                    return;
                }
            }
            GameObject gameObject = collectedItemList.ViewComponent.UiTransform.gameObject.AddChild(collectedItemList.PrefabItems.gameObject);
            if (gameObject == null)
            {
                return;
            }
            gameObject.SetActive(true);
            transform = gameObject.transform.Find("Negative");
            if (transform != null)
            {
                transform.gameObject.SetActive(false);
            }
            uilabel = gameObject.transform.GetComponentInChildren<UILabel>();
            if (uilabel == null)
            {
                uilabel = gameObject.transform.GetComponent<UILabel>();
            }
            if (uilabel != null)
            {
                uilabel.text = string.Format(config.notificationText, 1, killed);
            }
            UISprite component = gameObject.transform.Find("Icon").GetComponent<UISprite>();
            if (component != null)
            {
                component.atlas = collectedItemList.xui.GetAtlasByName("UIAtlas", config.notificationIcon);
                component.spriteName = config.notificationIcon;
                component.color = Color.white;
            }
            gameObject.transform.localPosition = new Vector3(gameObject.transform.localPosition.x, (float)collectedItemList.items.Count * collectedItemList.height + (float)collectedItemList.yOffset, gameObject.transform.localPosition.z);
            XUiC_CollectedItemList.Data data = new XUiC_CollectedItemList.Data();
            data.Item = gameObject;
            data.TimeAdded = Time.time;
            data.ItemStack = null;
            data.count = 1;
            data.uiAtlasIcon = config.notificationIcon;
            collectedItemList.items.Add(data);
            if (collectedItemList.items.Count > 12)
            {
                collectedItemList.removeLastEntry(0);
            }
        }
        public void LoadConfig()
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

    }
}
