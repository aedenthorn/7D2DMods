using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ClearQuestDebug
{
    public class ClearQuestDebug : IModApi
    {

        public static ModConfig config;
        public static ClearQuestDebug context;
        public static Mod mod;
        public static bool showingSleepers;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        [HarmonyPatch(typeof(ConsoleCmdSleeper), nameof(ConsoleCmdSleeper.Execute))]
        static class ConsoleCmdSleeper_Execute_Patch
        {
            static bool Prefix(List<string> _params, CommandSenderInfo _senderInfo)
            {
                if (!config.modEnabled || _params.Count == 0)
                    return true;
                switch (_params[0].ToLower())
                {
                    case "ss":
                        ShowSleepers();
                        return false;
                    case "sv":
                        ShowSleeperVolumes();
                        return false;
                    /*
                    case "d":
                        DespawnSleeperVolumes();
                        return false;
                    */
                    case "c":
                        ClearSleeperVolumes(_params);
                        return false;
                }
                return true;
            }
            private static void ShowSleepers()
            {
                showingSleepers = !showingSleepers;
                Dbgl($"showing sleepers: {showingSleepers}");
            }
            private static void ShowSleeperVolumes()
            {
                var world = GameManager.Instance.World;
                if (world == null)
                {
                    return;
                }
                Dbgl($"showing sleeper events: {QuestEventManager.instance.SleeperVolumeUpdateDictionary.Count}");

                foreach (var kvp in QuestEventManager.instance.SleeperVolumeUpdateDictionary)
                {
                    for (int i = 0; i < kvp.Value.EntityList.Count; i++)
                    {
                        Dbgl($"\t\tshowing sleeper volumes: {kvp.Value.SleeperVolumes.Count}");
                        EntityPlayer entityPlayer = GameManager.Instance.World.GetEntity(kvp.Value.EntityList[i]) as EntityPlayer;
                        for (int j = 0; j < kvp.Value.SleeperVolumes.Count; j++)
                        {
                            if (entityPlayer is EntityPlayerLocal)
                            {
                                QuestEventManager.Current.SleeperVolumePositionAdded(kvp.Value.SleeperVolumes[j].Center);
                            }
                            else
                            {
                                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageQuestEvent>().Setup(NetPackageQuestEvent.QuestEventTypes.ShowSleeperVolume, kvp.Value.EntityList[i], kvp.Value.SleeperVolumes[j].Center), false, kvp.Value.EntityList[i], -1, -1, null, 192);
                            }
                        }
                    }
                }
            }
            private static void DespawnSleeperVolumes()
            {

                var world = GameManager.Instance.World;
                if (world == null)
                {
                    return;
                }

                foreach (var value in QuestEventManager.instance.SleeperVolumeUpdateDictionary.Values.ToArray())
                {
                    for (int i = 0; i < value.EntityList.Count; i++)
                    {
                        if (GameManager.Instance.World.GetEntity(value.EntityList[i]) is EntityPlayerLocal)
                        {
                            Dbgl($"\t\tdespawning sleeper volumes: {value.SleeperVolumes.Count}");
                            for (int j = 0; j < value.SleeperVolumes.Count; j++)
                            {
                                value.SleeperVolumes[j].respawnMap?.Clear();
                                value.SleeperVolumes[j].respawnList?.Clear();
                                value.SleeperVolumes[j].Despawn(world);
                            }
                            break;
                        }
                    }
                }
            }
            private static void ClearSleeperVolumes(List<string> _params)
            {

                var world = GameManager.Instance.World;
                if (world == null)
                {
                    return;
                }

                foreach (var value in QuestEventManager.instance.SleeperVolumeUpdateDictionary.Values.ToArray())
                {
                    for (int i = 0; i < value.EntityList.Count; i++)
                    {
                        if(
                            (_params.Count > 1 && GameManager.Instance.World.GetEntity(value.EntityList[i]) is EntityPlayer && (GameManager.Instance.World.GetEntity(value.EntityList[i]) as EntityPlayer).cachedPlayerName.AuthoredName.Text == _params[1]) || 
                            (_params.Count == 1 && GameManager.Instance.World.GetEntity(value.EntityList[i]) is EntityPlayerLocal))
                        {
                            Dbgl($"\tclearing sleeper volumes: {value.SleeperVolumes.Count}");
                            for (int j = 0; j < value.SleeperVolumes.Count; j++)
                            {
                                value.SleeperVolumes[j].wasCleared = true;
                            }
                            break;
                        }
                    }
                }
            }

        }
        [HarmonyPatch(typeof(NavObject), nameof(NavObject.IsValidEntity))]
        public static class NavObject_IsValidEntity_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling NavObject.IsValidEntity");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.IsSleeperPassive)))
                    {
                        Dbgl($"Overriding check for passive sleeping");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ClearQuestDebug), nameof(ClearQuestDebug.OverrideIsSleeperPassive))));
                        i++;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        private static bool OverrideIsSleeperPassive(bool result)
        {
            if (!config.modEnabled || !showingSleepers)
            {
                return result;
            }
            return false;
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
