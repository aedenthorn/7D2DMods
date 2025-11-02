using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace AdvancedCruiseControl
{
    public class AdvancedCruiseControl : IModApi
    {

        public static ModConfig config;
        public static AdvancedCruiseControl context;
        public static Mod mod;
        
        public enum CruiseState
        {
            breaking,
            coasting,
            cruising,
            accelerating
        }

        public static CruiseState currentCruiseState;
        public static float currentCruiseAccel;
        public static double currentCruiseSpeedPercent = 1.0;
        public static bool currentTurboState;
        public static bool wasRunning;
        
        public static float timeSinceLastSecond;
        public static float accumulatedFuelUse;
        public static int currentFuelEconomy;

        private static List<Type> cruiseVehicles = new List<Type>()
        {
            typeof(EntityBicycle),
            typeof(EntityMinibike),
            typeof(EntityMotorcycle),
            typeof(EntityVGyroCopter),
            typeof(EntityVBlimp),
            typeof(EntityVJeep)
        };

        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            if (!config.modEnabled)
                return;
            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

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


        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.Update))]
        static class EntityPlayerLocal_Update_Patch
        {
            public static void Postfix(EntityPlayerLocal __instance)
            {
                if (!config.modEnabled)
                    return;
                if (__instance.AttachedToEntity == null || !cruiseVehicles.Contains(__instance.AttachedToEntity.GetType()))
                {
                    currentTurboState = false;
                    currentCruiseAccel = 0;
                    currentCruiseState = CruiseState.coasting;
                    currentCruiseSpeedPercent = 1;
                    return;
                }
                if(GameManager.Instance.isAnyCursorWindowOpen(null))
                {
                    return;
                }
                if (AedenthornUtils.CheckKeyDown(config.toggleKey))
                {
                    Dbgl($"cruise  toggled");
                    config.cruiseEnabled = !config.cruiseEnabled;
                    SaveConfig();
                    GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.toggleText, config.cruiseEnabled.ToString()), true);
                }
                if (AedenthornUtils.CheckKeyDown(config.accelKey))
                {
                    currentCruiseSpeedPercent = Math.Round(Math.Min(currentCruiseSpeedPercent + 0.1, config.maxSpeedMult), 1);
                    Dbgl($"cruise speed factor set to {currentCruiseSpeedPercent}");
                    GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.speedText, currentCruiseSpeedPercent), true);
                }
                else if (AedenthornUtils.CheckKeyDown(config.decelKey))
                {
                    currentCruiseSpeedPercent = Math.Round(Math.Max(currentCruiseSpeedPercent - 0.1, 0.1), 1);
                    Dbgl($"cruise speed factor set to {currentCruiseSpeedPercent}");
                    GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.speedText, currentCruiseSpeedPercent), true);
                }
            }
        }
        
        [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.MoveByAttachedEntity))]
        static class EntityVehicle_MoveByAttachedEntity_Patch
        {

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                if (!config.modEnabled)
                    return codes;
                Dbgl("Transpiling EntityVehicle.MoveByAttachedEntity");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Stfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(MovementInput), nameof(MovementInput.moveForward)))
                    {
                        var labels = codes[i].ExtractLabels();
                        Dbgl("Adding method to override move forward");
                        codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AdvancedCruiseControl), nameof(AdvancedCruiseControl.OverrideMoveForward))));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                        var ci = new CodeInstruction(OpCodes.Ldarg_0);
                        ci.labels = labels;
                        codes.Insert(i, ci);
                        i += 3;
                    }
                    else if (codes[i].opcode == OpCodes.Stfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(MovementInput), nameof(MovementInput.running)))
                    {
                        Dbgl("Adding method to override running");
                        var labels = codes[i].ExtractLabels();
                        codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AdvancedCruiseControl), nameof(AdvancedCruiseControl.OverrideRunning))));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                        var ci = new CodeInstruction(OpCodes.Ldarg_0);
                        ci.labels = labels;
                        codes.Insert(i, ci);
                        i += 3;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static float OverrideMoveForward(float result, EntityVehicle vehicle, EntityPlayerLocal player)
        {
            if (!config.cruiseEnabled || !cruiseVehicles.Contains(vehicle.GetType()))
                return result;
            LocalPlayerUI uiforPlayer = LocalPlayerUI.GetUIForPlayer(player);
            PlayerActionsVehicle vehicleActions = uiforPlayer.playerInput.VehicleActions;

            if (vehicleActions.Move.Y > 0)
            {
                currentCruiseState = CruiseState.accelerating;
                currentCruiseAccel = vehicleActions.Move.Y;
            }
            else if (vehicleActions.Move.Y == 0)
            {
                if (currentCruiseState == CruiseState.accelerating)
                {
                    currentCruiseState = CruiseState.cruising;
                    Dbgl("from accel to cruise");
                }
                else if (currentCruiseState != CruiseState.cruising)
                {
                    currentCruiseState = CruiseState.coasting;
                    currentCruiseAccel = 0;
                }
            }
            else
            {
                Dbgl("breaking");
                currentCruiseState = CruiseState.breaking;
                currentCruiseAccel = vehicleActions.Move.Y;
            }
            return currentCruiseAccel;
        }
        private static bool OverrideRunning(bool result, EntityVehicle vehicle, EntityPlayerLocal player)
        {
            if (!config.cruiseEnabled || !cruiseVehicles.Contains(vehicle.GetType()) || currentCruiseState < CruiseState.cruising)
            {
                wasRunning = false;
                currentTurboState = false;
                return result;
            }

            if (player.movementInput.running && !wasRunning)
            {
                currentTurboState =  !currentTurboState;
            }
            wasRunning = player.movementInput.running;

            return currentTurboState;
        }

        [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.PhysicsFixedUpdate))]
        static class EntityVehicle_PhysicsFixedUpdate_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                if (!config.modEnabled)
                    return codes;
                Dbgl("Transpiling EntityVehicle.PhysicsFixedUpdate");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(Vehicle), nameof(Vehicle.VelocityMaxForward)))
                    {
                        Dbgl("Adding method to override velocity max forward");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AdvancedCruiseControl), nameof(AdvancedCruiseControl.OverrideVelocityMaxForward))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                        i += 3;
                    }
                    else if (codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo && (FieldInfo)codes[i].operand == AccessTools.Field(typeof(Vehicle), nameof(Vehicle.VelocityMaxTurboForward)))
                    {
                        Dbgl("Adding method to override velocity max turbo forward");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AdvancedCruiseControl), nameof(AdvancedCruiseControl.OverrideVelocityMaxTurboForward))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0));
                        i += 3;
                    }
                }

                return codes.AsEnumerable();
            }
        }
        private static float OverrideVelocityMaxForward(float result, EntityVehicle vehicle)
        {
            if (!config.cruiseEnabled || !cruiseVehicles.Contains(vehicle.GetType()))
            {
                return result;
            }
            result *= (float)currentCruiseSpeedPercent;

            return result;
        }
        private static float OverrideVelocityMaxTurboForward(float result, EntityVehicle vehicle)
        {
            if (!config.cruiseEnabled || !cruiseVehicles.Contains(vehicle.GetType()))
            {
                return result;
            }
            result *= (float)currentCruiseSpeedPercent;

            return result;
        }
        [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.FireEvent), new Type[] { typeof(VehiclePart.Event), typeof(VehiclePart), typeof(float) })]
        static class Vehicle_FireEvent_Patch
        {
            public static void Prefix(Vehicle __instance, VehiclePart.Event _event, VehiclePart _fromPart, ref float _arg)
            {
                if (!config.modEnabled || !cruiseVehicles.Contains(__instance.entity.GetType()) || _event != VehiclePart.Event.FuelRemove)
                    return;
                _arg *= config.fuelConsumeMult;

                if (config.showFuelEconomy)
                {
                    timeSinceLastSecond += Time.deltaTime;
                    accumulatedFuelUse += _arg;
                    if (timeSinceLastSecond >= 1)
                    {
                        currentFuelEconomy = Mathf.RoundToInt(__instance.GetFuelLevel() / accumulatedFuelUse / 60f);
                        //Dbgl($"fuel economy: {currentFuelEconomy}min, ({accumulatedFuelUse}), _arg {_arg}, consumePercent {consumePercent}, fuel left {__instance.GetFuelLevel()}");
                        timeSinceLastSecond = 0;
                        accumulatedFuelUse = 0;
                    }
                }
            }
        }
        [HarmonyPatch(typeof(XUiC_HUDStatBar), nameof(XUiC_HUDStatBar.GetBindingValueInternal))]
        static class XUiC_VehicleStats_GetBindingValue_Patch
        {
            public static void Postfix(XUiC_HUDStatBar __instance, ref string value, string bindingName)
            {
                if (!config.modEnabled || !config.showFuelEconomy || !bindingName.Equals("statcurrentwithmax") || __instance.statType != HUDStatTypes.VehicleFuel || __instance.Vehicle == null || !cruiseVehicles.Contains(__instance.Vehicle.GetType()))
                {
                    return;
                }
                if(currentFuelEconomy > 0)
                {
                    value += $" ({currentFuelEconomy}min)";
                }
            }
        }
    }
}
