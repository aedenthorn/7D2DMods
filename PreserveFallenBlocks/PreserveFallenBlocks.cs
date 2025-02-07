using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace PreserveFallenBlocks
{
    public class PreserveFallenBlocks : IModApi
    {

        public static ModConfig config;
        public static PreserveFallenBlocks context;
        public static Mod mod;
        public static Dictionary<EntityFallingBlock, Block> fallingDict = new Dictionary<EntityFallingBlock, Block>();
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        [HarmonyPatch(typeof(World), nameof(World.LetBlocksFall))]
        static class ConsoleCmdSleeper_Execute_Patch
        {
            static bool Prefix(World __instance)
            {
                if (!config.modEnabled)
                    return true;
                if (__instance.fallingBlocks.Count == 0)
                {
                    return false;
                }
                int num = 0;
                Vector3i zero = Vector3i.zero;
                while (__instance.fallingBlocks.Count > 0 && num < 2)
                {
                    Vector3i vector3i = __instance.fallingBlocks.Dequeue();
                    if (zero.Equals(vector3i))
                    {
                        __instance.fallingBlocks.Enqueue(vector3i);
                        return false;
                    }
                    __instance.fallingBlocksMap.Remove(vector3i);
                    BlockValue block = __instance.GetBlock(vector3i.x, vector3i.y, vector3i.z);
                    if (!block.isair)
                    {
                        if (IsAllowed(block.Block.blockName))
                        {
                            long texture = __instance.GetTexture(vector3i.x, vector3i.y, vector3i.z);
                            block.Block.OnBlockStartsToFall(__instance, vector3i, block);
                            DynamicMeshManager.ChunkChanged(vector3i, -1, block.type);
                            Vector3 vector = new Vector3((float)vector3i.x + 0.5f + __instance.RandomRange(-0.1f, 0.1f), (float)vector3i.y + 0.5f, (float)vector3i.z + 0.5f + __instance.RandomRange(-0.1f, 0.1f));
                            EntityFallingBlock entityFallingBlock = (EntityFallingBlock)EntityFactory.CreateEntity(EntityClass.FromString("fallingBlock"), -1, block, texture, 1, vector, Vector3.zero, -1f, -1, null, -1, "");
                            __instance.SpawnEntityInWorld(entityFallingBlock);
                            num++;
                        }
                        else
                        {
                            long texture = __instance.GetTexture(vector3i.x, vector3i.y, vector3i.z);
                            Block block2 = block.Block;
                            block2.OnBlockStartsToFall(__instance, vector3i, block);
                            DynamicMeshManager.ChunkChanged(vector3i, -1, block.type);
                            if (block2.ShowModelOnFall())
                            {
                                Vector3 vector = new Vector3((float)vector3i.x + 0.5f + __instance.RandomRange(-0.1f, 0.1f), (float)vector3i.y + 0.5f, (float)vector3i.z + 0.5f + __instance.RandomRange(-0.1f, 0.1f));
                                EntityFallingBlock entityFallingBlock = (EntityFallingBlock)EntityFactory.CreateEntity(EntityClass.FromString("fallingBlock"), -1, block, texture, 1, vector, Vector3.zero, -1f, -1, null, -1, "");
                                __instance.SpawnEntityInWorld(entityFallingBlock);
                                num++;
                            }
                        }
                    }
                }
                return false;
            }

        }
        [HarmonyPatch(typeof(Entity), nameof(Entity.SetDead))]
        static class Entity_SetDead_Patch
        {
            static void Prefix(Entity __instance)
            {
                if (!config.modEnabled || !(__instance is EntityFallingBlock) || !IsAllowed((__instance as EntityFallingBlock).blockValue.Block.blockName))
                    return;
                var bv = (__instance as EntityFallingBlock).blockValue;
                
                __instance.world.SetBlockRPC(__instance.GetBlockPosition(), );

            }

        }
        private static bool IsAllowed(string name)
        {
            return config.allowedTypes.Contains(name) || !config.ignoreTypes.Contains(name);
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
