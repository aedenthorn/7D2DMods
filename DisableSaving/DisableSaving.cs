using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DisableSaving
{
    public class DisableSaving : IModApi
    {

        public static ModConfig config;
        public static DisableSaving context;
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


        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveLocalPlayerData))]
        static class GameManager_SaveLocalPlayerData_Patch
        {
            static bool Prefix()
            {
                if (!config.modEnabled || config.savingEnabled)
                    return true;
                Dbgl("Prevented saving player data");
                return false;
            }
        }
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveWorld))]
        static class GameManager_SaveWorld_Patch
        {
            static bool Prefix()
            {
                if (!config.modEnabled || config.savingEnabled)
                    return true;
                Dbgl("Prevented saving world");
                return false;
            }
        }
        [HarmonyPatch(typeof(World), nameof(World.Save))]
        static class World_Save_Patch
        {
            static bool Prefix()
            {
                if (!config.modEnabled || config.savingEnabled)
                    return true;
                Dbgl("Prevented saving world - this shouldn't be necessary");
                return false;
            }
        }
        [HarmonyPatch(typeof(WorldState), nameof(WorldState.Save), new Type[] { typeof(string) } )]
        static class WorldState_Save_Patch1
        {
            static bool Prefix()
            {
                if (!config.modEnabled || config.savingEnabled)
                    return true;
                Dbgl("Prevented saving world state");
                return false;
            }
        }
        [HarmonyPatch(typeof(WorldState), nameof(WorldState.Save), new Type[] { typeof(Stream) } )]
        static class WorldState_Save_Patch2
        {
            static bool Prefix()
            {
                if (!config.modEnabled || config.savingEnabled)
                    return true;
                Dbgl("Prevented saving world state");
                return false;
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
        static class GameManager_Update_Patch
        {
            static void Postfix()
            {
                if (!config.modEnabled)
                    return;
                if (AedenthornUtils.CheckKeyDown(config.toggleKey))
                {
                    config.savingEnabled = !config.savingEnabled;
                    Dbgl($"Pressed toggle key; enabled: {config.savingEnabled}");
                    SaveConfig();
                    if(GameManager.Instance.World?.GetPrimaryPlayer() != null)
                        GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.toggleText, config.savingEnabled), string.Empty, null, null, true);
                }
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

        public static void SaveConfig()
        {
            var path = Path.Combine(mod.Path, "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
