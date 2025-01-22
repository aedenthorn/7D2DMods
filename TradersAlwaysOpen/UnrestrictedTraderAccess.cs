using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace UnrestrictedTraderAccess
{
    public class UnrestrictedTraderAccess : IModApi
    {

        public static ModConfig config;
        public static UnrestrictedTraderAccess context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }


        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;

                if (AedenthornUtils.CheckKeyDown(config.toggleKey) && !___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsModalWindowOpen())
                {
                    Dbgl($"Pressed toggle key");
                    config.removeBuildProtection = !config.removeBuildProtection;
                    GameManager.ShowTooltip(___m_World.GetPrimaryPlayer(), config.removeBuildProtection ? config.ProtectionDisabledText : config.ProtectionEnabledText);
                    SaveConfig();
                }
            }
        }

        [HarmonyPatch(typeof(TraderInfo), nameof(TraderInfo.IsWarningTime))]
        [HarmonyPatch(MethodType.Getter)]
        static class TraderInfo_IsWarningTime_Patch
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled)
                    return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(TraderInfo), nameof(TraderInfo.IsOpen))]
        [HarmonyPatch(MethodType.Getter)]
        static class TraderInfo_IsOpen_Patch
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled)
                    return true;

                __result = true;
                return false;
            }
        }
        
        [HarmonyPatch(typeof(TraderArea), nameof(TraderArea.SetClosed))]
        static class TraderArea_SetClosed_Patch
        {

            static void Prefix(TraderArea __instance, ref bool _bClosed)
            {
                if (!config.modEnabled)
                    return;
                _bClosed = false;
            }
        }
        
        [HarmonyPatch(typeof(TraderArea), nameof(TraderArea.IsWithinTeleportArea))]
        static class TraderArea_IsWithinTeleportArea_Patch
        {

            static bool Prefix(TraderArea __instance, ref bool __result)
            {
                if (!config.modEnabled)
                    return true;

                __instance.IsClosed = false;
                __result = false;
                return false;
            }
        }
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.checkForTeleportOutOfTraderArea))]
        static class EntityAlive_checkForTeleportOutOfTraderArea_Patch
        {

            static bool Prefix()
            {
                if (!config.modEnabled)
                    return true;

                return false;
            }
        }

        [HarmonyPatch(typeof(World), nameof(World.SetupTraders))]
        static class World_SetupTraders_Patch
        {

            static void Prefix(World __instance)
            {
                if (!config.modEnabled || World.traderAreas == null) 
                    return;
                var _bClosed = false;
                for (int x = 0; x < World.traderAreas.Count; x++)
                {
                    var area = World.traderAreas[x];
                    area.IsClosed = false;
                    int num = World.toChunkXZ(area.Position.x - 1);
                    int num2 = World.toChunkXZ(area.Position.x + area.PrefabSize.x + 1);
                    int num3 = World.toChunkXZ(area.Position.z - 1);
                    int num4 = World.toChunkXZ(area.Position.z + area.PrefabSize.z + 1);
                    for (int i = num3; i <= num4; i++)
                    {
                        for (int j = num; j <= num2; j++)
                        {
                            if (!(__instance.GetChunkSync(j, i) is Chunk))
                            {
                                goto next;
                            }
                        }
                    }
                    for (int k = num3; k <= num4; k++)
                    {
                        for (int l = num; l <= num2; l++)
                        {
                            Chunk chunk = __instance.GetChunkSync(l, k) as Chunk;
                            List<Vector3i> list = chunk.IndexedBlocks["TraderOnOff"];
                            if (list != null)
                            {
                                for (int m = 0; m < list.Count; m++)
                                {
                                    BlockValue block = chunk.GetBlock(list[m]);
                                    if (!block.ischild)
                                    {
                                        Vector3i vector3i = chunk.ToWorldPos(list[m]);
                                        if (area.ProtectBounds.Contains(vector3i))
                                        {
                                            Block block2 = block.Block;
                                            if (block2 is BlockDoor)
                                            {
                                                if (_bClosed && BlockDoor.IsDoorOpen(block.meta))
                                                {
                                                    block2.OnBlockActivated(__instance, 0, vector3i, block, null);
                                                }
                                                BlockDoorSecure blockDoorSecure = block2 as BlockDoorSecure;
                                                if (blockDoorSecure != null)
                                                {
                                                    if (_bClosed)
                                                    {
                                                        if (!blockDoorSecure.IsDoorLocked(__instance, vector3i))
                                                        {
                                                            block2.OnBlockActivated("lock", __instance, 0, vector3i, block, null);
                                                        }
                                                    }
                                                    else if (blockDoorSecure.IsDoorLocked(__instance, vector3i))
                                                    {
                                                        block2.OnBlockActivated("unlock", __instance, 0, vector3i, block, null);
                                                    }
                                                }
                                            }
                                            else if (block2 is BlockLight)
                                            {
                                                block.meta = (byte)((!_bClosed) ? ((int)(block.meta | 2)) : ((int)block.meta & -3));
                                                __instance.SetBlockRPC(vector3i, block);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                next:
                    continue;
                }
            }
        }


        [HarmonyPatch(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i) })]
        static class World_IsWithinTraderArea_Patch1
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled || !config.removeBuildProtection)
                    return true;
                __result = false;
                return false;
            }
        }
        
        [HarmonyPatch(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i), typeof(Vector3i) })]
        static class World_IsWithinTraderArea_Patch2
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled || !config.removeBuildProtection)
                    return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(World), nameof(World.IsWithinTraderPlacingProtection), new Type[] { typeof(Vector3i) })]
        static class World_IsWithinTraderPlacingProtection_Patch1
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled || !config.removeBuildProtection)
                    return true;
                __result = false;
                return false;
            }
        }
        [HarmonyPatch(typeof(World), nameof(World.IsWithinTraderPlacingProtection), new Type[] { typeof(Bounds) })]
        static class World_IsWithinTraderPlacingProtection_Patch2
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled || !config.removeBuildProtection)
                    return true;
                __result = false;
                return false;
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
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }

    }
}
