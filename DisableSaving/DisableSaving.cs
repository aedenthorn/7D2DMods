using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        public static bool savingEnabled = true;

        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }


        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveLocalPlayerData))]
        public static class GameManager_SaveLocalPlayerData_Patch
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                Dbgl("Prevented saving player data");
                return false;
            }
        }
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveWorld))]
        public static class GameManager_SaveWorld_Patch
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                Dbgl("Prevented saving world");
                return false;
            }
        }
        [HarmonyPatch(typeof(World), nameof(World.Save))]
        public static class World_Save_Patch
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                Dbgl("Prevented saving world - this shouldn't be necessary");
                return false;
            }
        }
        [HarmonyPatch(typeof(World), nameof(World.SaveWorldState))]
        public static class World_SaveWorldState_Patch
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                Dbgl("Prevented saving world state");
                return false;
            }
        }
        [HarmonyPatch(typeof(Chunk), nameof(Chunk.NeedsSaving))]
        [HarmonyPatch(MethodType.Getter)]
        public static class Chunk_NeedsSaving_Patch
        {
            public static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                __result = false;
                return false;
            }
        }
        [HarmonyPatch(typeof(MultiBlockManager), nameof(MultiBlockManager.SaveIfDirty))]
        public static class MultiBlockManager_SaveIfDirty_Patch
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                return false;
            }
        }
        [HarmonyPatch(typeof(StreamUtils), nameof(StreamUtils.WriteStreamToFile), new Type[] { typeof(Stream), typeof(string) })]
        public static class StreamUtils_WriteStreamToFile_Patch1
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                Dbgl("Prevented saving stream");
                return false;
            }
        }
        [HarmonyPatch(typeof(StreamUtils), nameof(StreamUtils.WriteStreamToFile), new Type[] { typeof(Stream), typeof(string), typeof(int) })]
        public static class StreamUtils_WriteStreamToFile_Patch2
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled)
                    return true;
                Dbgl("Prevented saving stream");
                return false;
            }
        }
        
        [HarmonyPatch(typeof(GamePrefs), nameof(GamePrefs.Save), new Type[] { })]
        public static class GamePrefs_Save_Patch1
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled || !config.includeGamePrefs)
                    return true;
                Dbgl("Prevented saving prefs");
                return false;
            }
        }
        [HarmonyPatch(typeof(GamePrefs), nameof(GamePrefs.Save), new Type[] { typeof(string) })]
        public static class GamePrefs_Save_Patch2
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled || !config.includeGamePrefs)
                    return true;
                Dbgl("Prevented saving prefs");
                return false;
            }
        }
        [HarmonyPatch(typeof(GamePrefs), nameof(GamePrefs.Save), new Type[] { typeof(string), typeof(List<EnumGamePrefs>) })]
        public static class GamePrefs_Save_Patch3
        {
            public static bool Prefix()
            {
                if (!config.modEnabled || savingEnabled || !config.includeGamePrefs)
                    return true;
                Dbgl("Prevented saving prefs");
                return false;
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
        public static class GameManager_Update_Patch
        {
            public static void Postfix(World ___m_World)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;
                if (AedenthornUtils.CheckKeyDown(config.toggleKey))
                {
                    savingEnabled = !savingEnabled;
                    Dbgl($"Pressed toggle key; enabled: {savingEnabled}");
                    if(GameManager.Instance.World?.GetPrimaryPlayer() != null)
                        GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.toggleText, savingEnabled), string.Empty, null, null, true);
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
