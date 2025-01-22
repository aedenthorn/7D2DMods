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
        private static List<Vector3i> storageList = new List<Vector3i>();
        private static Dictionary<Vector3i, TEFeatureStorage> storageDict = new Dictionary<Vector3i, TEFeatureStorage>();
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
