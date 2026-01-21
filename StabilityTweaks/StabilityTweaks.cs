using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace StabilityTweaks
{
    public class StabilityTweaks : IModApi
    {
        public static ModConfig config;
        public static StabilityTweaks context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;

            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }


        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Update))]
        static class EntityPlayerLocal_Update_Patch
        {
            public static void Postfix(EntityPlayerLocal __instance)
            {
                if (!config.modEnabled || GameManager.Instance.isAnyCursorWindowOpen(null) || __instance.inventory?.IsHoldingBlock() != true)
                {
                    return;
                }
                if (AedenthornUtils.CheckKeyDown(config.increaseKey))
                {
                    if (config.stabilityModifier >= 1)
                    {
                        config.stabilityModifier = Math.Max(1, config.stabilityModifier + (AedenthornUtils.CheckKeyHeld(config.modKey) ? 1 : 0.1f));
                    }
                    else
                    {
                        config.stabilityModifier += 0.1f;
                    }

                    SaveConfig();
                    Dbgl($"stability mod set to {config.stabilityModifier}");
                }
                if (AedenthornUtils.CheckKeyDown(config.decreaseKey))
                {
                    if(config.stabilityModifier > 1)
                    {
                        config.stabilityModifier = Math.Max(1, config.stabilityModifier - (AedenthornUtils.CheckKeyHeld(config.modKey) ? 1 : 0.1f));
                    }
                    else
                    {
                        config.stabilityModifier = Math.Max(0.1f, config.stabilityModifier - 0.1f);
                    }
                    SaveConfig();
                    Dbgl($"stability mod set to {config.stabilityModifier}");
                }
            }
        }

        [HarmonyPatch(typeof(World), nameof(World.AddFallingBlock))]
        public static class World_AddFallingBlocks_Patch
        {

            static bool Prefix()
            {
                if (!config.modEnabled || config.stabilityModifier > 0)
                    return true;
                return false;
            }
        }
        [HarmonyPatch(typeof(ModManager), nameof(ModManager.LoadMods))]
        public static class ModManager_LoadMods_Patch
        {

            static void Postfix()
            {
                LoadConfig();
            }
        }
        [HarmonyPatch(typeof(Chunk), nameof(Chunk.SetStability))] 
        public static class Chunk_SetStability_Patch
        {

            public static void Prefix(ref byte _v)
            {
                if (!config.modEnabled || config.stabilityModifier <= 0)
                    return;
                _v = Math.Max((byte)2, _v);
            }
        }
        [HarmonyPatch(typeof(StabilityCalculator), nameof(StabilityCalculator.CalcPhysicsStabilityToFall))]
        public static class StabilityCalculator_CalcPhysicsStabilityToFall_Patch
        {
            public static bool aPrefix(StabilityCalculator __instance, Vector3i _pos, int maxBlocksToCheck, ref float calculatedStability, ref List<Vector3i> __result)
            {
                if (!config.modEnabled || config.stabilityModifier < 0)
                    return true;
                List<Vector3i> list = null;
                calculatedStability = 0f;
                __instance.unstablePositions.Clear();
                __instance.unstablePositions.Add(_pos);
                __instance.positionsToCheck.Clear();
                __instance.positionsToCheck.Enqueue(_pos);
                __instance.uniqueUnstablePositions.Clear();
                int num = 0;
                int num2 = 0;
                IChunk chunk = null;
                int i = 0;
                while (i < maxBlocksToCheck)
                {
                    int num3 = num;
                    foreach (Vector3i vector3i in __instance.positionsToCheck)
                    {
                        StabilityCalculator.world.GetChunkFromWorldPos(vector3i, ref chunk);
                        BlockValue blockValue = ((chunk != null) ? chunk.GetBlockNoDamage(World.toBlockXZ(vector3i.x), vector3i.y, World.toBlockXZ(vector3i.z)) : BlockValue.Air);
                        Block block = blockValue.Block;
                        num2 += block.blockMaterial.Mass.Value;
                        foreach (Vector3i vector3i2 in Vector3i.AllDirectionsShuffled)
                        {
                            Vector3i vector3i3 = vector3i + vector3i2;
                            if (chunk == null || chunk.X != World.toChunkXZ(vector3i3.x) || chunk.Z != World.toChunkXZ(vector3i3.z))
                            {
                                chunk = StabilityCalculator.world.GetChunkFromWorldPos(vector3i3);
                            }
                            int num4 = World.toBlockXZ(vector3i3.x);
                            int num5 = World.toBlockXZ(vector3i3.z);
                            BlockValue blockValue2 = ((chunk != null) ? chunk.GetBlockNoDamage(num4, vector3i3.y, num5) : BlockValue.Air);
                            int num6 = (int)((!blockValue2.isair && chunk != null) ? chunk.GetStability(num4, vector3i3.y, num5) : 0);
                            if (num6 == 15)
                            {
                                int forceToOtherBlock = blockValue.GetForceToOtherBlock(blockValue2);
                                if (vector3i2.y == -1)
                                {
                                    num3 = 100000;
                                }
                                else
                                {
                                    num3 += forceToOtherBlock;
                                }
                                num += forceToOtherBlock;
                            }
                            else if (((num6 > 0 && blockValue2.Block.StabilitySupport) || num6 > 1) && __instance.unstablePositions.Add(vector3i3))
                            {
                                __instance.uniqueUnstablePositions.Enqueue(vector3i3);
                                if (vector3i2.y == -1)
                                {
                                    num3 = 100000;
                                }
                                else
                                {
                                    num3 += blockValue.GetForceToOtherBlock(blockValue2);
                                }
                            }
                        }
                    }
                    if (num3 > 0)
                    {
                        calculatedStability = 1f - (float)num2 / (float)num3;
                    }
                    if (num2 > num3)
                    {
                        list = __instance.unstablePositions.Except(__instance.uniqueUnstablePositions).ToList<Vector3i>();
                        if (list.Count == 0)
                        {
                            calculatedStability = 1f;
                            break;
                        }
                        break;
                    }
                    else
                    {
                        if (__instance.uniqueUnstablePositions.Count == 0)
                        {
                            break;
                        }
                        __instance.positionsToCheck.Clear();
                        Queue<Vector3i> queue = __instance.uniqueUnstablePositions;
                        __instance.uniqueUnstablePositions = __instance.positionsToCheck;
                        __instance.positionsToCheck = queue;
                        __instance.uniqueUnstablePositions.Clear();
                        i++;
                    }
                }
                __result = list;
                return false;
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                Dbgl("Transpiling StabilityCalculator.CalcPhysicsStabilityToFall");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(BlockValue), nameof(BlockValue.GetForceToOtherBlock)))
                    {
                        Dbgl("Adding method to modify force to other block");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StabilityTweaks), nameof(StabilityTweaks.GetStabilityMod))));
                        i++;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        [HarmonyPatch(typeof(StabilityCalculator), nameof(StabilityCalculator.GetBlockStability), new Type[] { typeof(Vector3i), typeof(BlockValue) })]
        public static class StabilityCalculator_GetBlockStability_Patch
        {
            public static bool aPrefix(ref float __result, Vector3i _pos, BlockValue _newBV)
            {
                if (!config.modEnabled || config.stabilityModifier < 0)
                    return true;
                StabilityCalculator.posChecked.Clear();
                StabilityCalculator.posToCheck.Clear();
                StabilityCalculator.posToCheckNext.Clear();
                if (!Block.BlocksLoaded)
                {
                    __result = 1f;
                    return false;
                }
                if (!GameManager.bPhysicsActive)
                {
                    __result = 1f;
                    return false;
                }
                BlockValue blockValue = (StabilityCalculator.posPlaced.ContainsKey(_pos) ? _newBV : StabilityCalculator.world.GetBlock(_pos));
                int totalTotalForce = 0;
                int mass = 0;
                StabilityCalculator.posChecked.Add(_pos);
                StabilityCalculator.posToCheck.Add(_pos);
                int num3 = 0;
                int num4 = 0;
                float num5 = 0f;
                new Vector3(0f, -1f, 0f);
                IChunk chunk = null;
                while (num4 < 25 && StabilityCalculator.posToCheck.Count > 0)
                {
                    num4++;
                    int totalForce = totalTotalForce;
                    int i = 0;
                    while (i < StabilityCalculator.posToCheck.Count)
                    {
                        Vector3i vector3i = StabilityCalculator.posToCheck[i];
                        int num7;
                        BlockValue blockValue2;
                        if (StabilityCalculator.posPlaced.TryGetValue(vector3i, out num7))
                        {
                            blockValue2 = _newBV;
                            goto IL_010F;
                        }
                        StabilityCalculator.world.GetChunkFromWorldPos(vector3i.x, vector3i.z, ref chunk);
                        if (chunk != null)
                        {
                            blockValue2 = chunk.GetBlockNoDamage(vector3i.x & 15, vector3i.y, vector3i.z & 15);
                            goto IL_010F;
                        }
                    IL_0284:
                        i++;
                        continue;
                    IL_010F:
                        num3 += 7;
                        mass += blockValue2.Block.blockMaterial.Mass.Value;
                        Dbgl($"{blockValue2.Block.blockName} mass {blockValue2.Block.blockMaterial.Mass.Value}, total {mass}");
                        Vector3i[] allDirectionsShuffled = Vector3i.AllDirectionsShuffled;
                        for (int j = 0; j < allDirectionsShuffled.Length; j++)
                        {
                            Vector3i vector3i2 = vector3i + allDirectionsShuffled[j];
                            if (vector3i2.y >= 0)
                            {

                                int stability;
                                BlockValue blockValue3;
                                if (StabilityCalculator.posPlaced.TryGetValue(vector3i2, out stability))
                                {
                                    blockValue3 = _newBV;
                                    Dbgl($"other block posPlaced {_newBV.Block?.blockName}, no stability");
                                }
                                else
                                {
                                    StabilityCalculator.world.GetChunkFromWorldPos(vector3i2.x, vector3i2.z, ref chunk);
                                    if (chunk == null)
                                    {
                                        goto IL_0273;
                                    }
                                    int num8 = vector3i2.x & 15;
                                    int num9 = vector3i2.z & 15;
                                    blockValue3 = chunk.GetBlockNoDamage(num8, vector3i2.y, num9);
                                    stability = (int)chunk.GetStability(num8, vector3i2.y, num9);
                                    Dbgl($"other block {blockValue3.Block?.blockName} stability {stability}");
                                }
                                if (stability >= 0)
                                {
                                    if (stability == 15)
                                    {
                                        int forceToOtherBlock = GetStabilityMod(blockValue2.GetForceToOtherBlock(blockValue3));
                                        if (allDirectionsShuffled[j].y == -1)
                                        {
                                            totalForce = 100000;
                                        }
                                        else
                                        {
                                            totalForce += forceToOtherBlock;
                                        }
                                        totalTotalForce += forceToOtherBlock;
                                        Dbgl($"stability block force to other {forceToOtherBlock}, total force {totalForce}, totalTotalForce {totalTotalForce}");
                                    }
                                    else if ((stability > 1 || blockValue3.Block.StabilitySupport) && StabilityCalculator.posChecked.Add(vector3i2))
                                    {
                                        StabilityCalculator.posToCheckNext.Add(vector3i2);
                                        int forceToOtherBlock;
                                        if (allDirectionsShuffled[j].y == -1)
                                        {
                                            totalForce = 100000;
                                            Dbgl($"instability ground {totalForce}");
                                        }
                                        else
                                        {
                                            forceToOtherBlock = GetStabilityMod(blockValue2.GetForceToOtherBlock(blockValue3));
                                            totalForce += GetStabilityMod(blockValue2.GetForceToOtherBlock(blockValue3));
                                            Dbgl($"instability block force to other {forceToOtherBlock}, total force {totalForce}");
                                        }
                                    }
                                }
                                Dbgl($"total force {totalForce}");
                            }
                        IL_0273:;
                        }
                        goto IL_0284;
                    }
                    if (mass > totalForce)
                    {
                        StabilityViewer.GetBlocks += num3;
                        StabilityViewer.TotalIterations += num4;
                        __result = 0f;

                        Dbgl($"total force {totalForce} < mass {mass}");
                        return false;
                    }
                    if (totalForce > 0)
                    {
                        num5 = Mathf.Max(num5, (float)mass / ((float)totalForce * 1.01f));
                        Dbgl($"total force {totalForce} >= mass {mass}, highest instability {num5}");
                    }
                    List<Vector3i> list = StabilityCalculator.posToCheck;
                    StabilityCalculator.posToCheck = StabilityCalculator.posToCheckNext;
                    StabilityCalculator.posToCheckNext = list;
                    StabilityCalculator.posToCheckNext.Clear();
                }
                StabilityViewer.GetBlocks += num3;
                StabilityViewer.TotalIterations += num4;
                __result = 1f - num5;
                Dbgl($"result {__result}");
                return false;
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                Dbgl("Transpiling StabilityCalculator.GetBlockStability");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(BlockValue), nameof(BlockValue.GetForceToOtherBlock)))
                    {
                        Dbgl("Adding method to modify force to other block");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StabilityTweaks), nameof(StabilityTweaks.GetStabilityMod))));
                        i++;
                    }
                    //if (codes[i].opcode == OpCodes.Ldc_I4_S && (sbyte)codes[i].operand == 25)
                    //{
                    //    Dbgl("Adding method to modify number of blocks to check");
                    //    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StabilityTweaks), nameof(StabilityTweaks.GetStabilityBlocksMod))));
                    //    i++;
                    //}
                }

                return codes.AsEnumerable();
            }
        }

        public static int GetStabilityMod(int value)
        {
            if (!config.modEnabled || config.stabilityModifier < 0)
                return value;

            var newVal = Mathf.RoundToInt(Mathf.Clamp(value * config.stabilityModifier, 0, int.MaxValue));
            return newVal;
        }

        public static int GetStabilityBlocksMod(int value)
        {
            if (!config.modEnabled || config.stabilityModifier < 0)
                return value;
            var newVal = Mathf.RoundToInt(Mathf.Clamp(value * config.stabilityModifier, 25, int.MaxValue));
            Dbgl($"blocks to check {newVal}");
            return newVal;
        }

        public static void LoadConfig()
        {
            var path = Path.Combine(mod.Path, "config.json");
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
