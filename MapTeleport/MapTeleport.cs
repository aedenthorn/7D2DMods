using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace MapTeleport
{
    public class MapTeleport : IModApi
    {

        public static ModConfig config;
        public static MapTeleport context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }
        [HarmonyPatch(typeof(World), nameof(World.SetupTraders))]
        static class World_SetupTraders_Patch
        {

            static void Postfix()
            {
                if (!config.modEnabled || GameStats.GetBool(EnumGameStats.IsTeleportEnabled))
                    return;
                Dbgl("Enabling teleport");
                GameStats.SetObject(EnumGameStats.IsTeleportEnabled, true);
            }
        }
        
        [HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.teleportPlayerOnMap))]
        static class XUiC_MapArea_teleportPlayerOnMap_Patch
        {

            static void Postfix()
            {
                if (!config.modEnabled)
                    return;
                LocalPlayerUI.GetUIForPlayer(GameManager.Instance.World.GetPrimaryPlayer()).windowManager.CloseAllOpenWindows(null, false);

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
