using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace KillNotification
{
    public class KillNotification : IModApi
    {

        public static ModConfig config;
        public static KillNotification context;
        public static Mod mod;

        public static AudioClip killChime;
        public static bool customChime;
        public static string customChimePath;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            foreach (var f in Directory.GetFiles(modInstance.Path))
            {
                if (Path.GetFileNameWithoutExtension(f).ToLower() == "chime" && new List<string>() { ".wav", ".mp3", ".m4a", ".ogg" }.Contains(Path.GetExtension(f)))
                {
                    Dbgl($"Found custom chime file at {f}");
                    customChimePath = f;
                    var chimeGo = new GameObject("KillChimeLoader");
                    var wcl = chimeGo.AddComponent<KillChimeLoader>();
                    break;
                }
            }
        }


        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.AwardKill))]
        static class EntityAlive_AwardKill_Patch
        {
            static void Postfix(EntityAlive __instance, EntityAlive killer)
            {
                if (!config.modEnabled)
                    return;
                var isKiller = killer == GameManager.Instance.World.GetPrimaryPlayer();
                if (!config.notifyAllKills && !isKiller)
                    return;
                if (config.chimeAllKills || isKiller)
                {
                    if (killChime != null)
                    {
                        Manager.PlayXUiSound(killChime, config.chimeVolume);
                    }
                    else if (!customChime && !string.IsNullOrEmpty(config.notificationSound))
                    {
                        if (!PlayInsidePlayerHead(config.notificationSound, -1, 0f, false, false))
                        {
                            Dbgl($"failed to play kill chime {config.notificationSound}");
                        }
                    }
                }
                AddIconNotification(Localization.Get(__instance.EntityName, false));

            }
        }
        public static bool PlayInsidePlayerHead(string soundGroupName, int entityID = -1, float delay = 0f, bool isLooping = false, bool isUnique = false)
        {
            Entity entity = ((entityID >= 0) ? GameManager.Instance.World.GetEntity(entityID) : null);
            Manager.ConvertName(ref soundGroupName, entity);
            XmlData xmlData;
            if (!Manager.audioData.TryGetValue(soundGroupName, out xmlData))
            {
                return false;
            }
            if (!xmlData.Update())
            {
                return false;
            }
            ClipSourceMap randomClip = xmlData.GetRandomClip();
            if (randomClip == null)
            {
                return false;
            }
            AudioSource audioSource = Manager.LoadAudio(randomClip.forceLoop, 0f, randomClip.clipName, randomClip.audioSourceName);
            if (audioSource == null)
            {
                return false;
            }
            GameObject gameObject = audioSource.gameObject;
            Transform transform = audioSource.transform;
            EntityAlive entityAlive = Manager.LocalPlayer();
            Transform transform2 = entityAlive.transform;
            transform.SetParent(transform2, false);
            transform.position = transform2.position;
            audioSource.volume = config.chimeVolume;
            Manager.SetPitch(audioSource, xmlData, config.chimePitch);
            if (xmlData.vibratesController)
            {
                GameManager.Instance.triggerEffectManager.SetAudioRumbleSource(audioSource, xmlData.vibrationStrengthMultiplier, false);
            }
            if (isUnique)
            {
                if (Manager.uniqueSrc != null)
                {
                    Manager.StopSource(Manager.uniqueSrc);
                }
                Manager.uniqueSrc = audioSource;
            }
            new PlayAndCleanup(gameObject, audioSource, 0f, delay, false, false);
            if (GamePrefs.GetBool(EnumGamePrefs.OptionsSubtitlesEnabled) && randomClip.hasSubtitle)
            {
                GameManager.ShowSubtitle(LocalPlayerUI.primaryUI.xui, Manager.GetFormattedSubtitleSpeaker(randomClip.subtitleID), Manager.GetFormattedSubtitle(randomClip.subtitleID), audioSource.clip.length, false);
            }
            return true;
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
                            uilabel.text = config.notificationTextPlural.Replace("{name}", killed).Replace("{number}", collectedItemList.items[i].count.ToString());
                            collectedItemList.items[i].TimeAdded = Time.time;
                            return;
                        }
                    }
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
                uilabel.text = config.notificationTextSingle.Replace("{name}", killed).Replace("{number}", "1");
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
