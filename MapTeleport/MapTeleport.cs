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

            static void Prefix(XUiC_MapArea __instance, Vector3 _screenPosition)
            {
                if (!config.modEnabled)
                    return;
                Vector3 playerDest = __instance.screenPosToWorldPos(_screenPosition, false);
                
                foreach (var d in DroneManager.Instance.dronesActive)
                {
                    if (d.belongsToPlayerId(__instance.localPlayer.entityId))
                    {
                        Vector3 distance = (d.position - __instance.localPlayer.position);
                        if (distance.magnitude > d.FollowDistance)
                            continue;
                        Vector3 vector = playerDest + distance;
                        d.teleportToPosition(vector);
                    }
                }
                LocalPlayerUI.GetUIForPlayer(__instance.xui.playerUI.entityPlayer).windowManager.CloseAllOpenWindows(null, false);

            }
        }

        public void LoadConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            if (!File.Exists(path))
            {
                config = new ModConfig();
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            else
            {
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
            }
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
