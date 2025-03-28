﻿using HarmonyLib;
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
                    if (sed.EntityList.Exists(e => GameManager.Instance.World.GetEntity(e) is EntityPlayerLocal))
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
        //[HarmonyPatch(typeof(SleeperEventData), nameof(SleeperEventData.Update))]
        public static class SleeperEventData_Update_Patch
        {
            public static void Postfix(SleeperEventData __instance)
            {
                if (!config.modEnabled || !AedenthornUtils.CheckKeyDown(config.showAllPointsKey))
                    return;
                for (int l = 0; l < __instance.EntityList.Count; l++)
                {
                    EntityPlayer entityPlayer = GameManager.Instance.World.GetEntity(__instance.EntityList[l]) as EntityPlayer;
                    for (int m = 0; m < __instance.SleeperVolumes.Count; m++)
                    {
                        if (entityPlayer is EntityPlayerLocal)
                        {
                            QuestEventManager.Current.SleeperVolumePositionAdded(__instance.SleeperVolumes[m].Center);
                        }
                        else
                        {
                            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuestEvent>().Setup(NetPackageQuestEvent.QuestEventTypes.ShowSleeperVolume, __instance.EntityList[l], __instance.SleeperVolumes[m].Center), false, __instance.EntityList[l], -1, -1, null, 192);
                        }
                    }
                }
            }
        }

    }
}
