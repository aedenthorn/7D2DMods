using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using UnityEngine;

namespace ShowRemainingToClear
{
    public class ShowRemainingToClear : IModApi
    {
        private static ShowRemainingToClear context;
        private static Mod mod;
        public static ModConfig config;
        public void InitMod(Mod modInstance)
        {
            LoadConfig();

            context = this;
            mod = modInstance;
            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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
            if (config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

        [HarmonyPatch(typeof(XUiC_QuestTrackerObjectiveEntry), nameof(XUiC_QuestTrackerObjectiveEntry.GetBindingValue))]
        public static class XUiC_QuestObjectiveEntry_GetBindingValue_Patch
        {
            public static bool Prefix(XUiC_QuestTrackerObjectiveEntry __instance, ref string value, string bindingName)
            {
                if (!config.modEnabled || !(__instance.QuestObjective is ObjectiveClearSleepers) || bindingName != "objectivedescription")
                    return true;

                //var spawnPointList = AccessTools.Field(typeof(SleeperVolume), "spawnPointList");

                int left = 0;
                foreach (var sed in QuestEventManager.Current.SleeperVolumeUpdateDictionary.Values)
                {
                    if (sed.EntityList.Exists(e => (GameManager.Instance.World.GetEntity(e) as EntityPlayer) is EntityPlayerLocal))
                    {
                        left = sed.SleeperVolumes.Count();
                        break;
                    }
                }
                if (left <= 0)
                    return true;

                value = string.Format(config.remainingText, __instance.QuestObjective.Description, left);
                return false;
            }
        }
    }
}
