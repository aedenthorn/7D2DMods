using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
                if (mi.DeclaringType == t)
                {
                    Dbgl($"Patching {t.Name}.GetBlockActivationCommands");
                    harmony.Patch(
                        mi,
                        postfix: new HarmonyMethod(typeof(PickupBlocks), nameof(GetBlockActivationCommands))
                    );

                    mi = t.GetMethod("OnBlockActivated", new Type[] { typeof(string), typeof(WorldBase), typeof(int), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) });
                    if (mi.DeclaringType == t)
                    {
                        foreach (var p in mi.GetParameters())
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
        private static bool CanPickupBlock(BlockActivationCommand[] result)
        {
            return config.EnableMod && config.EnablePickup && (!config.EnableMenuRestrict || (result != null && result.Where(c => c.enabled && c.text != "take").Count() > 0));
        }
        private static bool CanPickupBlock(WorldBase _world, Vector3i _blockPos)
        {
            return config.EnableMod && config.EnablePickup && (!config.EnableLandClaimRestrict || _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer(), false));
        }
        public static void GetBlockActivationCommands(WorldBase _world, Vector3i _blockPos, ref BlockActivationCommand[] __result)
        {
            if (!config.EnableMod || _world.IsEditor())
                return;
            List<BlockActivationCommand> temp = new List<BlockActivationCommand>(__result);
            AddTakeCommand(_world, _blockPos, temp);
            __result = temp.ToArray();
        }


        public static bool OnBlockActivatedOne(Block __instance, string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player)
        {
            if (!config.EnableMod || _blockValue.ischild || _commandName != "take" || !CanPickupBlock(_world, _blockPos) || !CanPickupBlock(_blockValue.Block.cmds))
                return true;
            TakeBlock(_world, _cIdx, _blockPos, _blockValue, _player);
            return false;
        }
        
        public static bool OnBlockActivatedTwo(Block __instance, string _commandName, WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player)
        {
            if (!config.EnableMod || _blockValue.ischild || _commandName != "take" || !CanPickupBlock(_world, _blockPos) || !CanPickupBlock(_blockValue.Block.cmds))
                return true;
            TakeBlock(_world, _clrIdx, _blockPos, _blockValue, _player);
            return false;
        }
        private static void TempCanPickup_Prefix(Block __instance, ref bool __state, WorldBase _world, Vector3i _blockPos)
        {
            if (!config.EnableMod)
                return;
            __state = __instance.CanPickup;
            if (!__state)
                __instance.CanPickup = CanPickupBlock(_world, _blockPos) && CanPickupBlock(__instance.cmds);

        }
        private static void TempCanPickup_Postfix(Block __instance, bool __state)
        {
            if (!config.EnableMod)
                return;
            __instance.CanPickup = __state;
        }

        private static void AddTakeCommand(WorldBase _world, Vector3i _blockPos, List<BlockActivationCommand> temp)
        {
            bool enabled = CanPickupBlock(_world, _blockPos) && CanPickupBlock(temp.ToArray());
            for (int i = 0; i < temp.Count; i++)
            {
                if (temp[i].text == "take")
                {
                    if(!temp[i].enabled)
                        temp[i] = new BlockActivationCommand(temp[i].text, temp[i].icon, enabled, temp[i].highlighted);
                    return;
                }
            }
            temp.Add(new BlockActivationCommand("take", "hand", enabled, false));
        }
        private static void TakeBlock(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player)
        {
            Block block = _blockValue.Block;
            var entity = _world.GetTileEntity(_clrIdx, _blockPos);
            if (entity?.GetType().Equals(typeof(TileEntityLootContainer)) == true)
            {
                var telc = entity as TileEntityLootContainer;
                 if (telc != null && !telc.IsEmpty() || !telc.bTouched)
                {
                    Dbgl($"{block.blockName} is telc, blocked because empty: {telc.IsEmpty()}, touched {telc.bTouched}");
                    GameManager.ShowTooltip(_player as EntityPlayerLocal, config.EmptyFirstMessage, string.Empty, "ui_denied", null);
                    return;
                }
            }
            if(entity is TileEntityComposite tec)
            {
                Dbgl($"is tec");
                var lootable = tec.GetFeature<ITileEntityLootable>() as TEFeatureStorage;
                if(lootable != null && (!lootable.bTouched || !lootable.IsEmpty()))
                {
                    Dbgl($"storage blocked because empty: {lootable.IsEmpty()}, touched: {lootable.bTouched}");
                    GameManager.ShowTooltip(_player as EntityPlayerLocal, config.EmptyFirstMessage, string.Empty, "ui_denied", null);
                    return;
                }
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

            //block.CanPickup = true;
            block.PickedUpItemValue = block.Properties.Params1[Block.PropCanPickup];
            QuestEventManager.Current.BlockPickedUp(block.GetBlockName(), _blockPos);
            QuestEventManager.Current.ItemAdded(itemStack);
            _world.GetGameManager().PickupBlockServer(_clrIdx, _blockPos, _blockValue, _player.entityId, null);
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        public static class GameManager_Update_Patch
        {

            public static void Postfix(World ___m_World)
            {
                if (___m_World is null || ___m_World.GetPrimaryPlayer() is null || GameManager.Instance.isAnyCursorWindowOpen(null))
                    return;
                if (AedenthornUtils.CheckKeyDown(config.ToggleKey))
                {
                    Dbgl($"Pressed mod toggle key");
                    if (AedenthornUtils.CheckKeyHeld(config.ToggleLandRestrictModKey))
                    {
                        config.EnableLandClaimRestrict = !config.EnableLandClaimRestrict;
                        GameManager.ShowTooltip(___m_World.GetPrimaryPlayer(), config.EnableLandClaimRestrict ? config.LandEnabledText : config.LandDisabledText, true);
                    }
                    else if (AedenthornUtils.CheckKeyHeld(config.ToggleMenuModKey))
                    {
                        config.EnableMenuRestrict = !config.EnableMenuRestrict;
                        GameManager.ShowTooltip(___m_World.GetPrimaryPlayer(), config.EnableMenuRestrict ? config.MenuEnabledText: config.MenuDisabledText, true);
                    }
                    else 
                    {
                        config.EnablePickup = !config.EnablePickup;
                        GameManager.ShowTooltip(___m_World.GetPrimaryPlayer(), config.EnablePickup ? config.EnabledText : config.DisabledText, true);
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
