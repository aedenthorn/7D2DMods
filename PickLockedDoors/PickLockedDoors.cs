using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PickLockedDoors
{
    public class PickLockedDoors : IModApi
    {
        private static PickLockedDoors context;
        private static Mod mod;
        public static ModConfig config;
        public static Dictionary<Vector3i, float> pickTimeDict = new Dictionary<Vector3i, float>();
        public void InitMod(Mod modInstance)
        {
            config = new ModConfig();

            LoadConfig();

            context = this;
            mod = modInstance;
            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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
        [HarmonyPatch(typeof(GameManager), "StartGame")]
        static class GameManager_StartGame_Patch
        {
            static void Prefix()
            {
                pickTimeDict.Clear();
            }
        }
        [HarmonyPatch(typeof(BlockDoorSecure), nameof(BlockDoorSecure.GetBlockActivationCommands))]
        static class BlockDoorSecure_GetBlockActivationCommands_Patch
        {
            static void Postfix(WorldBase _world, int _clrIdx, Vector3i _blockPos, ref BlockActivationCommand[] __result)
            {
                if (!config.modEnabled || _world.IsEditor())
                    return;
                TileEntitySecureDoor tileEntitySecureDoor = (TileEntitySecureDoor)_world.GetTileEntity(_clrIdx, _blockPos);

                if (tileEntitySecureDoor == null) 
                    return;

                List<BlockActivationCommand> temp = new List<BlockActivationCommand>(__result);
                var bac = new BlockActivationCommand("pick", "unlock", tileEntitySecureDoor.IsLocked(), false);
                temp.Add(bac);
                __result = temp.ToArray();
                
            }
        }
        [HarmonyPatch(typeof(BlockDoorSecure), nameof(BlockDoorSecure.OnBlockActivated))]
        static class BlockDoorSecure_OnBlockActivated_Patch
        {

            static void Postfix(WorldBase _world, string _commandName, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _player, ref bool __result)
            {
                if (!config.modEnabled || _commandName != "pick")
                    return;

                Dbgl("Starting to pick door lock");

                if (!_blockValue.Block.Properties.Values.TryGetValue("LockPickItem", out string lockPickItem))
                    lockPickItem = config.lockPickItem;
                float lockPickBreakChance;
                if (!_blockValue.Block.Properties.Values.TryGetValue("LockPickBreakChance", out string breakString))
                {
                    lockPickBreakChance = config.lockPickBreakChance;
                }
                else
                {
                    lockPickBreakChance = StringParsers.ParseFloat(breakString, 0, -1, NumberStyles.Any);
                }
                float lockPickTime;
                if (!_blockValue.Block.Properties.Values.TryGetValue("LockPickTime", out string timeString))
                {
                    lockPickTime = config.lockPickTime;
                }
                else
                {
                    lockPickTime = StringParsers.ParseFloat(timeString, 0, -1, NumberStyles.Any);
                }
                if(!pickTimeDict.ContainsKey(_blockPos))
                    pickTimeDict[_blockPos] = lockPickTime; 

                LocalPlayerUI playerUI = (_player as EntityPlayerLocal).PlayerUI;
                ItemValue item = ItemClass.GetItem(lockPickItem, false);
                if (playerUI.xui.PlayerInventory.GetItemCount(item) == 0)
                {
                    playerUI.xui.CollectedItemList.AddItemStack(new ItemStack(item, 0), true);
                    GameManager.ShowTooltip(_player as EntityPlayerLocal, Localization.Get("ttLockpickMissing"));
                    __result = true;
                    return;
                }
                playerUI.windowManager.Open("timer", true, false, true);
                XUiC_Timer childByType = playerUI.xui.GetChildByType<XUiC_Timer>();
                TimerEventData timerEventData = new TimerEventData();
                timerEventData.CloseEvent += EventData_CloseEvent;
                float alternateTime = -1f;
                if (_player.rand.RandomRange(1f) < EffectManager.GetValue(PassiveEffects.LockPickBreakChance, _player.inventory.holdingItemItemValue, lockPickBreakChance, _player, null, default, true, true, true, true, true, 1, true, false))
                {
                    float value = EffectManager.GetValue(PassiveEffects.LockPickTime, _player.inventory.holdingItemItemValue, lockPickTime, _player, null, default, true, true, true, true, true, 1, true, false);
                    float num = value - ((pickTimeDict[_blockPos] == -1f) ? (value - 1f) : (pickTimeDict[_blockPos] + 1f));
                    alternateTime = _player.rand.RandomRange(num + 1f, value - 1f);
                }
                timerEventData.Data = new object[]
                {
                    _cIdx,
                    _blockValue,
                    _blockPos,
                    _player,
                    item
                };
                timerEventData.Event += EventData_Event;
                timerEventData.alternateTime = alternateTime;
                timerEventData.AlternateEvent += EventData_CloseEvent;
                childByType.SetTimer(EffectManager.GetValue(PassiveEffects.LockPickTime, _player.inventory.holdingItemItemValue, config.lockPickTime, _player, null, default, true, true, true, true, true, 1, true, false), timerEventData, pickTimeDict[_blockPos], "");
                Manager.BroadcastPlayByLocalPlayer(_blockPos.ToVector3() + Vector3.one * 0.5f, "Misc/unlocking");
                __result = true;
            }

            private static void EventData_Event(TimerEventData timerData)
            {
                World world = GameManager.Instance.World;
                object[] array = (object[])timerData.Data;
                int clrIdx = (int)array[0];
                BlockValue blockValue = (BlockValue)array[1];
                Vector3i blockPos = (Vector3i)array[2];
                BlockValue block = world.GetBlock(blockPos);
                EntityPlayerLocal entityPlayerLocal = array[3] as EntityPlayerLocal;
                TileEntitySecureDoor tileEntitySecureDoor = (TileEntitySecureDoor)world.GetTileEntity(clrIdx, blockPos);
                if (tileEntitySecureDoor == null)
                {
                    return;
                }
                tileEntitySecureDoor.SetLocked(false);
                Manager.BroadcastPlayByLocalPlayer(blockPos.ToVector3() + Vector3.one * 0.5f, "Misc/unlocking");
                Dbgl("Unlocked door");
                ResetEventData(timerData);
            }


            private static void EventData_CloseEvent(TimerEventData timerData)
            {
                World world = GameManager.Instance.World;
                object[] array = (object[])timerData.Data;
                int clrIdx = (int)array[0];
                BlockValue blockValue = (BlockValue)array[1];
                Vector3i blockPos = (Vector3i)array[2];
                EntityPlayerLocal entityPlayerLocal = array[3] as EntityPlayerLocal;
                ItemValue itemValue = array[4] as ItemValue;
                LocalPlayerUI uiforPlayer = LocalPlayerUI.GetUIForPlayer(entityPlayerLocal);

                TileEntitySecureDoor tileEntitySecureDoor = (TileEntitySecureDoor)world.GetTileEntity(clrIdx, blockPos);
                if (tileEntitySecureDoor == null)
                {
                    return;
                }

                Manager.BroadcastPlayByLocalPlayer(blockPos.ToVector3() + Vector3.one * 0.5f, "Misc/locked");
                ItemStack itemStack = new ItemStack(itemValue, 1);
                uiforPlayer.xui.PlayerInventory.RemoveItem(itemStack);
                GameManager.ShowTooltip(entityPlayerLocal, Localization.Get("ttLockpickBroken"));
                uiforPlayer.xui.CollectedItemList.RemoveItemStack(itemStack);
                float lockPickTime;
                if (!blockValue.Block.Properties.Values.TryGetValue("LockPickTime", out string timeString))
                {
                    lockPickTime = config.lockPickTime;
                }
                else
                {
                    lockPickTime = StringParsers.ParseFloat(timeString, 0, -1, NumberStyles.Any);
                }
                pickTimeDict[blockPos] = Mathf.Max(lockPickTime * 0.25f, timerData.timeLeft);
                ResetEventData(timerData);
            }
            private static void ResetEventData(TimerEventData timerData)
            {
                timerData.AlternateEvent -= EventData_CloseEvent;
                timerData.CloseEvent -= EventData_CloseEvent;
                timerData.Event -= EventData_Event;
            }
        }
    }
}
