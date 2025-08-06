using HarmonyLib;
using Newtonsoft.Json;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
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
            harmony.Patch(
                original: AccessTools.Method(typeof(Block), nameof(Block.DropItemsOnEvent)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(BlockDamage), nameof(BlockDamage.OnEntityCollidedWithBlock)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Explosion), nameof(Explosion.AttackBlocks)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(ItemActionDumpWater), nameof(ItemActionDumpWater.ExecuteAction)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(ItemActionRepair), nameof(ItemActionRepair.ExecuteAction)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(World), nameof(World.CanPickupBlockAt)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodTranspiler)) 
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(World), nameof(World.GetLandClaimOwner), new Type[] { typeof(Vector3i), typeof(PersistentPlayerData) }),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.LandClaimMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(World), nameof(World.GetLandClaimOwnerInParty), new Type[] { typeof(EntityPlayer), typeof(PersistentPlayerData) }),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.LandClaimMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(World), nameof(World.IsMyLandProtectedBlock)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.LandClaimMethodTranspiler)) 
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(World), nameof(World.IsEmptyPosition)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.PlaceMethodTranspiler)) 
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(World), nameof(World.CanPlaceBlockAt)),
                transpiler: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.PlaceMethodTranspiler)) 
            );
            //harmony.Patch(
            //    original: AccessTools.Method(typeof(WallVolume), nameof(WallVolume.SetMinMax)),
            //    prefix: new HarmonyMethod(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.WallVolume_SetMinMax_Prefix)) 
            //);
        }

        private static bool WallVolume_SetMinMax_Prefix(WallVolume __instance)
        {
            if (!config.modEnabled || !GameManager.Instance.World.IsWithinTraderArea(World.worldToBlockPos((__instance.BoxMin + __instance.BoxMax).ToVector3() * 0.5f)))
                return true;
            __instance.BoxMin = Vector3i.zero;
            __instance.BoxMax = Vector3i.zero;
            __instance.Center = Vector3.zero;
            return false;
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        public static class GameManager_Update_Patch
        {

            public static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || ___m_World == null || ___m_World.GetPrimaryPlayer() == null)
                    return;

                if (AedenthornUtils.CheckKeyDown(config.toggleKey) && !___m_World.GetPrimaryPlayer().PlayerUI.windowManager.IsModalWindowOpen())
                {
                    Dbgl($"Pressed toggle key");
                    LoadConfig();
                    config.removeDamageProtection = !config.removeDamageProtection;
                    GameManager.ShowTooltip(___m_World.GetPrimaryPlayer(), config.removeDamageProtection ? config.damageProtectionDisabledText : config.damageProtectionEnabledText, true);
                    SaveConfig();
                    var xui = ___m_World.GetPrimaryPlayer().PlayerUI.xui;
                    if (xui == null)
                    {
                        Dbgl($"no xui");
                    }
                    else
                    {
                        var window = xui.GetWindowByType<XUiC_Location>();
                        if (window == null)
                        {
                            Dbgl($"no window");
                        }
                        else { 
                            window.RefreshBindings(true);
                        }
                    }
                }
            }
        }
        public static IEnumerable<CodeInstruction> DamageMethodTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            if (!config.modEnabled)
                return codes;
            Dbgl("Transpiling damage method");
            for (int i = 0; i < codes.Count; i++)
            {
                if ((codes[i].opcode == OpCodes.Callvirt || codes[i].opcode == OpCodes.Call) && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i) }))
                {
                    Dbgl("Adding method to override damage protection");
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.DamageMethodOverride))));
                    break;
                }
            }

            return codes.AsEnumerable();
        }

        public static bool DamageMethodOverride(bool result)
        {

            if (!config.modEnabled || !result || !config.removeDamageProtection)
                return result;
            return false;
        }

        public static IEnumerable<CodeInstruction> LandClaimMethodTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            if (!config.modEnabled)
                return codes;
            Dbgl("Transpiling land claim method");
            for (int i = 0; i < codes.Count; i++)
            {
                if ((codes[i].opcode == OpCodes.Callvirt || codes[i].opcode == OpCodes.Call) && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i) }))
                {
                    Dbgl("Adding method to override land claim protection");
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.LandClaimMethodOverride))));
                    break;
                }
            }

            return codes.AsEnumerable();
        }

        public static bool LandClaimMethodOverride(bool result)
        {

            if (!config.modEnabled || !result || !config.removeLandClaimProtection)
                return result;
            return false;
        }
        public static IEnumerable<CodeInstruction> PlaceMethodTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            if (!config.modEnabled)
                return codes;
            Dbgl("Transpiling place method");
            for (int i = 0; i < codes.Count; i++)
            {
                if ((codes[i].opcode == OpCodes.Callvirt || codes[i].opcode == OpCodes.Call) && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i) }))
                {
                    Dbgl("Adding method to override place protection");
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.PlaceMethodOverride))));
                    break;
                }
            }

            return codes.AsEnumerable();
        }

        public static bool PlaceMethodOverride(bool result)
        {

            if (!config.modEnabled || !result || !config.removePlaceProtection)
                return result;
            return false;
        }

        [HarmonyPatch(typeof(ItemActionAttack), nameof(ItemActionAttack.Hit))]
        static class ItemActionAttack_Hit_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                if (!config.modEnabled)
                    return codes;
                Dbgl("Transpiling ItemActionAttack.Hit");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i) }))
                    {
                        Dbgl("Adding method to override damage protection");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.ItemActionAttackHitOverride))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_1));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        public static bool ItemActionAttackHitOverride(bool result, WorldRayHitInfo hitInfo, int _attackerEntityId)
        {

            if (!config.modEnabled || !result)
                return result;

            if (config.removeDamageProtection)
                return false;
            EntityAlive entityAlive = GameManager.Instance.World.GetEntity(_attackerEntityId) as EntityAlive;
            if (entityAlive is EntityPlayer && config.alwaysAllowPlayerDamage)
            {
                Dbgl($"Allowing damage by player");
                return false;
            }
            if (!(entityAlive is EntityPlayer) && config.neverAllowNonPlayerDamage)
            {
                Dbgl($"Preventing damage by non-player");
                return true;
            }
            Vector3i vector3i = hitInfo.hit.blockPos;
            var name = ItemClass.GetForId(GameManager.Instance.World.ChunkClusters[hitInfo.hit.clrIdx].GetBlock(vector3i).type).Name;
            LoadConfig();
            if (config.alwaysAllowDamageTypes.Length > 0)
            {
                foreach (var s in config.alwaysAllowDamageTypes)
                {
                    if (name.Equals(s) || (s.EndsWith("*") && name.StartsWith(s.Substring(0, s.Length - 1))))
                    {
                        Dbgl($"Allowing damage to {name}");
                        return false;
                    }
                }
            }
            Dbgl($"Preventing damage to {name}");
            return result;
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

        [HarmonyPatch(typeof(World), nameof(World.CanPlaceLandProtectionBlockAt))]
        static class World_CanPlaceLandProtectionBlockAt_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                if (!config.modEnabled)
                    return codes;
                Dbgl("Transpiling World.CanPlaceLandProtectionBlockAt");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(World), nameof(World.IsWithinTraderArea), new Type[] { typeof(Vector3i), typeof(Vector3i) }))
                    {
                        Dbgl("Adding method to override land claim protection");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnrestrictedTraderAccess), nameof(UnrestrictedTraderAccess.CanPlaceLandProtectionBlockAtOverride))));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        public static bool CanPlaceLandProtectionBlockAtOverride(bool result)
        {
            if(!config.modEnabled || !config.removeLandClaimProtection) 
                return result;
            return false;
        }




        [HarmonyPatch(typeof(World), nameof(World.IsWithinTraderPlacingProtection), new Type[] { typeof(Vector3i) })]
        static class World_IsWithinTraderPlacingProtection_Patch1
        {

            static bool Prefix(ref bool __result)
            {
                if (!config.modEnabled || !config.removePlaceProtection)
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
                if (!config.modEnabled || !config.removePlaceProtection)
                    return true;
                __result = false;
                return false;
            }
        }
        
        [HarmonyPatch(typeof(XUiC_Location), nameof(XUiC_Location.GetBindingValue))]
        static class XUiC_Location_GetBindingValue_Patch
        {

            static void Postfix(XUiC_Location __instance, ref string _value, string _bindingName)
            {
                if (!config.modEnabled || !config.removeDamageProtection || _bindingName != "locationname" || __instance.lastPrefab == null || !__instance.lastPrefab.PrefabName.StartsWith("trader_"))
                    return;
                _value += config.locationDamageSuffix;
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
