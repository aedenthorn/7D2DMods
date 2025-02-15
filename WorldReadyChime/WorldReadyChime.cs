using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace WorldReadyChime
{
    public class WorldReadyChime : IModApi
    {

        public static ModConfig config;
        public static WorldReadyChime context;
        public static Mod mod;
        public static AudioClip spawnSound;

        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }



        [HarmonyPatch(typeof(XUi), nameof(XUi.Init))]
        static class XUi_Init_Patch
        {
            static void Postfix(XUi __instance)
            {
                if (!config.modEnabled)
                    return;
                __instance.LoadData<AudioClip>(config.chimeName, delegate (AudioClip o)
                {
                    spawnSound = o;
                });
            }
        }
        [HarmonyPatch(typeof(XUiC_SpawnSelectionWindow), nameof(XUiC_SpawnSelectionWindow.Open))]
        static class XUiC_SpawnSelectionWindow_Open_Patch
        {
            static void Prefix(bool _enteringGame)
            {
                if (!config.modEnabled || !_enteringGame)
                    return;
                Dbgl($"playing {config.chimeName}");
                Manager.PlayXUiSound(spawnSound, config.chimeVolume);
            }
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

    }
}
