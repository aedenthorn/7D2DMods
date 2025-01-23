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
using static UnityDistantTerrain;

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
                    Dbgl($"Patching {t.Name}.GetBlockActivationCommands");
                    harmony.Patch(
                        mi,
                        postfix: new HarmonyMethod(typeof(PickupBlocks), nameof(GetBlockActivationCommands))
                    );

                    mi = t.GetMethod("OnBlockActivated", new Type[] { typeof(string), typeof(WorldBase), typeof(int), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) });
                    if (mi.DeclaringType == t)
                    {
                        foreach(var p in mi.GetParameters())
                        {
                            if (p.Name == "_cIdx")
                            {
                                harmony.Patch(
                                    mi,
                                    new HarmonyMethod(typeof(PickupBlocks), nameof(OnBlockActivatedOne))
                                );
                                Dbgl($"Patching {t.Name}.OnBlockActivated");
                                break;
                            }
                            if (p.Name == "_clrIdx")
                            {
                                harmony.Patch(
                                    mi,
                                    new HarmonyMethod(typeof(PickupBlocks), nameof(OnBlockActivatedTwo))
                                );
                                Dbgl($"Patching {t.Name}.OnBlockActivated");
                                break;
                            }
                        }
                    }
                    mi = t.GetMethod("GetActivationText");
                    if (mi.DeclaringType == t)
                    {
                        Dbgl($"Patching {t.Name}.GetActivationText");
                        harmony.Patch(
                            original: mi,
                            prefix: new HarmonyMethod(typeof(PickupBlocks), nameof(TempCanPickup_Prefix)),
                            postfix: new HarmonyMethod(typeof(PickupBlocks), nameof(TempCanPickup_Postfix))
                        );
                    }
                    mi = t.GetMethod("HasBlockActivationCommands");
                    if (mi.DeclaringType == t)
                    {
                        Dbgl($"Patching {t.Name}.HasBlockActivationCommands");
                        harmony.Patch(
                            original: mi,
                            prefix: new HarmonyMethod(typeof(PickupBlocks), nameof(TempCanPickup_Prefix)),
                            postfix: new HarmonyMethod(typeof(PickupBlocks), nameof(TempCanPickup_Postfix))
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
        public static bool OnBlockActivatedOne(Block __instance, string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player)
        {
            if (!config.modEnabled || _blockValue.ischild || _commandName != "take")
                return true;
            TakeBlock(_world, _cIdx, _blockPos, _blockValue, _player);
            return false;
        }
        
        public static bool OnBlockActivatedTwo(Block __instance, string _commandName, WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player)
        {
            if (!config.modEnabled || _blockValue.ischild || _commandName != "take")
                return true;
            TakeBlock(_world, _clrIdx, _blockPos, _blockValue, _player);
            return false;
        }
        private static void TempCanPickup_Prefix(Block __instance, ref bool __state, WorldBase _world, Vector3i _blockPos)
        {
            if (!config.modEnabled)
                return;
            __state = __instance.CanPickup;
            if (!__state)
                __instance.CanPickup = CheckCanPickup(_world, _blockPos);

        }
        private static void TempCanPickup_Postfix(Block __instance, bool __state)
        {
            if (!config.modEnabled)
                return;
            __instance.CanPickup = __state;
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
            TileEntityLootContainer tileEntityLootContainer = _world.GetTileEntity(_clrIdx, _blockPos) as TileEntityLootContainer;
            if (tileEntityLootContainer != null && (!tileEntityLootContainer.IsEmpty() || !tileEntityLootContainer.bTouched))
            {
                GameManager.ShowTooltip(_player as EntityPlayerLocal, config.EmptyFirstMessage, string.Empty, "ui_denied", null);
                return;
            }
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
            Dbgl($"Can pickup? {block.CanPickup}");

            //block.CanPickup = true;
            block.PickedUpItemValue = block.Properties.Params1[Block.PropCanPickup];
            QuestEventManager.Current.BlockPickedUp(block.GetBlockName(), _blockPos);
            QuestEventManager.Current.ItemAdded(itemStack);
            _world.GetGameManager().PickupBlockServer(_clrIdx, _blockPos, _blockValue, _player.entityId, null);
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(World ___m_World)
            {
                if (___m_World is null || ___m_World.GetPrimaryPlayer() is null)
                    return;
                if (AedenthornUtils.CheckKeyDown(config.ToggleModKey))
                {
                    Dbgl($"Pressed mod toggle key");
                    GameManager.Instance.ClearTooltips(___m_World.GetPrimaryPlayer().PlayerUI.nguiWindowManager);
                    if (!config.modEnabled)
                    {
                        config.modEnabled = true;
                        config.RestrictBlocksToLandClaimArea = true;
                        GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), config.RestrictionEnabledText);
                    }
                    else if (config.AllowToggleLandClaimRestriction)
                    {
                        if (config.RestrictBlocksToLandClaimArea)
                        {
                            GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), config.RestrictionDisabledText);
                            config.RestrictBlocksToLandClaimArea = false;
                        }
                        else
                        {
                            GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), config.DisabledText);
                            config.RestrictBlocksToLandClaimArea = true;
                            config.modEnabled = false;
                        }
                    }
                    else 
                    {
                        config.modEnabled = false;
                        GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), config.DisabledText);
                    }
                    SaveConfig();
                }
            }
        }
        public static void SaveConfig()
        {
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
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
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }
    }
}
