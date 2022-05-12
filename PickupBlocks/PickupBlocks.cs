using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PickupBlocks
{
    public class PickupBlocks : IModApi
    {
        private static PickupBlocks context;
        private static Mod mod;
        public static ModConfig config;
        public void InitMod(Mod modInstance)
        {
            config = new ModConfig();
            LoadConfig();

            context = this;
            mod = modInstance;
            Harmony harmony = new Harmony(GetType().ToString());

            IEnumerable<Type> addCommandTypes = typeof(GameManager).Assembly.GetTypes().Where(t => typeof(Block).IsAssignableFrom(t));
            Dbgl($"Types: {addCommandTypes.Count()}");
            foreach (Type t in addCommandTypes)
            {
                var mi = t.GetMethod("GetBlockActivationCommands");
                if(mi.DeclaringType == t)
                {
                    Dbgl($"Patching {t.Name}");
                    harmony.Patch(
                        mi,
                        postfix: new HarmonyMethod(typeof(PickupBlocks), nameof(PickupBlocks.GetBlockActivationCommands))
                    );

                    mi = t.GetMethod("OnBlockActivated", new Type[] { typeof(int), typeof(WorldBase), typeof(int), typeof(Vector3i), typeof(BlockValue), typeof(EntityAlive) });
                    if (mi.DeclaringType == t)
                    {
                        foreach(var p in mi.GetParameters())
                        {
                            if(p.Name == "_cIdx")
                            {
                                harmony.Patch(
                                    mi,
                                    new HarmonyMethod(typeof(PickupBlocks), nameof(PickupBlocks.OnBlockActivatedOne))
                                );
                                break;
                            }
                            if(p.Name == "_clrIdx")
                            {
                                harmony.Patch(
                                    mi,
                                    new HarmonyMethod(typeof(PickupBlocks), nameof(PickupBlocks.OnBlockActivatedTwo))
                                );
                                break;
                            }
                        }
                    }
                    mi = t.GetMethod("GetActivationText");
                    if (mi.DeclaringType == t)
                    {
                        harmony.Patch(
                            mi,
                            new HarmonyMethod(typeof(PickupBlocks), nameof(PickupBlocks.GetActivationText_Prefix)),
                            new HarmonyMethod(typeof(PickupBlocks), nameof(PickupBlocks.GetActivationText_Postfix))
                        );
                    }
                }
            }
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        public static void GetBlockActivationCommands(WorldBase _world, Vector3i _blockPos, ref BlockActivationCommand[] __result)
        {
            if (!config.modEnabled || _world.IsEditor())
                return;
            List<BlockActivationCommand> temp = new List<BlockActivationCommand>(__result);
            AddTakeCommand(_world, _blockPos, temp);
            __result = temp.ToArray();
        }
        public static bool OnBlockActivatedOne(int _indexInBlockActivationCommands, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player, BlockActivationCommand[] ___cmds)
        {
            if (!config.modEnabled || _blockValue.ischild || ___cmds[_indexInBlockActivationCommands].text != "take")
                return true;
            TakeBlock(_world, _cIdx, _blockPos, _blockValue, _player);
            return false;
        }
        
        public static bool OnBlockActivatedTwo(int _indexInBlockActivationCommands, WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player, BlockActivationCommand[] ___cmds)
        {
            if (!config.modEnabled || _blockValue.ischild || ___cmds[_indexInBlockActivationCommands].text != "take")
                return true;
            TakeBlock(_world, _clrIdx, _blockPos, _blockValue, _player);
            return false;
        }
        private static void GetActivationText_Prefix(Block __instance, ref bool __state, WorldBase _world, Vector3i _blockPos)
        {
            if (!config.modEnabled)
                return;
            __state = __instance.CanPickup;
            if (!__state)
                __instance.CanPickup = CheckCanPickup(_world, _blockPos);

        }
        private static void GetActivationText_Postfix(Block __instance, bool __state)
        {
            if (!config.modEnabled)
                return;
            __instance.CanPickup = __state;
        }


        public void LoadConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            if (File.Exists(path)) 
            { 
                config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(path));
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        public static void Dbgl(object str, bool prefix = true)
        {
            if (config.isDebug)
                Debug.Log((prefix ? mod.ModInfo.Name.Value + " " : "") + str);
        }
        private static void AddTakeCommand(WorldBase _world, Vector3i _blockPos, List<BlockActivationCommand> temp)
        {
            bool enabled = CheckCanPickup(_world, _blockPos);
            for (int i = 0; i < temp.Count; i++)
            {
                if (temp[i].text == "take")
                {
                    if(!temp[i].enabled)
                        temp[i] = new BlockActivationCommand(temp[i].text, temp[i].icon, enabled, temp[i].highlighted);
                    return;
                }
            }
            var bac = new BlockActivationCommand("take", "hand", enabled, false);
            temp.Add(bac);
        }
        private static bool CheckCanPickup(WorldBase _world, Vector3i _blockPos)
        {
            if (config.RestrictBlocksToLandClaimArea && !_world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer(), false))
                return false;
            return true;
        }
        private static void TakeBlock(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player)
        {
            Block block = _blockValue.Block;
            if (!_world.CanPickupBlockAt(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer()))
            {
                _player.PlayOneShot("keystone_impact_overlay", false);
                return;
            }
            if (_blockValue.damage > 0)
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttRepairBeforePickup"), string.Empty, "ui_denied", null);
                return;
            }
            ItemStack itemStack = block.OnBlockPickedUp(_world, _clrIdx, _blockPos, _blockValue, _player.entityId);
            if (!_player.inventory.CanTakeItem(itemStack) && !_player.bag.CanTakeItem(itemStack))
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("xuiInventoryFullForPickup"), string.Empty, "ui_denied", null);
                return;
            }
            QuestEventManager.Current.BlockPickedUp(block.GetBlockName(), _blockPos);
            QuestEventManager.Current.ItemAdded(itemStack);
            _world.GetGameManager().PickupBlockServer(_clrIdx, _blockPos, _blockValue, _player.entityId, null);
        }
    }
}
