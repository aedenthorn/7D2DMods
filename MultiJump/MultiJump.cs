using HarmonyLib;
using InControl;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Path = System.IO.Path;

namespace MultiJump
{
    public class MultiJump : IModApi
    {

        public static ModConfig config;
        public static MultiJump context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.MoveByInput))]
        static class EntityPlayerLocal_MoveByInput_Patch
        {

            static void Prefix(EntityPlayerLocal __instance)
            {
                if (!config.modEnabled || __instance.movementInput is null)
                    return;
                if (__instance.movementInput.jump && !__instance.inputWasJump)
                {
                    Dbgl($"Jump input");

                    __instance.onGround = true;
                    __instance.inAir = false;
                    __instance.bJumping = false;
                    __instance.wasJumping = false;
                    __instance.jumpTrigger = false;
                    __instance.jumpTicks = 200;
                    __instance.jumpStateTicks = Mathf.CeilToInt(__instance.jumpDelay);
                    __instance.m_vp_FPController.Player.Jump.m_Active = false;
                    __instance.m_vp_FPController.m_MotorThrottle.y = __instance.m_vp_FPController.MotorJumpForce / Time.timeScale;
                    __instance.m_vp_FPController.m_MotorJumpForceAcc = 0;
                    __instance.m_vp_FPController.Player.Jump.NextAllowedStartTime = 0;

                }
            }

        }
        [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.StartJump))]
        static class EntityAlive_StartJump_Patch
        {
            static void Prefix(EntityAlive __instance)
            {
                if (!config.modEnabled || !(__instance is EntityPlayer player))
                    return;
                Dbgl($"Start jump {__instance.jumpState}, {__instance.accumulatedRootMotion}, {__instance.jumpDistance}, {__instance.jumpHeightDiff}, {__instance.jumpStateTicks}, {__instance.jumpDelay}");
                __instance.motion.y = EffectManager.GetValue(PassiveEffects.JumpStrength, null, player.jumpStrength, player, null, default(FastTags<TagGroup.Global>), true, true, true, true, true, 1, true, false) * player.Stats.Stamina.ValuePercent;

            }
        }
        [HarmonyPatch(typeof(vp_FPController), nameof(vp_FPController.UpdateJump))]
        static class vp_FPController_UpdateJump_Patch
        {
            static void Prefix(vp_FPController __instance)
            {
                if (!config.modEnabled)
                    return;

            }
        }
        [HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.OnUpdateLive))]
        static class EntityPlayerLocal_OnUpdateLive_Patch
        {

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling EntityPlayerLocal.OnUpdateLive");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(vp_Activity), nameof(vp_Activity.TryStart)))
                    {
                        Dbgl("Replacing jump trystart method");
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = AccessTools.Method(typeof(MultiJump), nameof(MultiJump.TryStart));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static bool TryStart(vp_Activity jump, bool value)
        {
            if(!config.modEnabled) 
                return jump.TryStart(value);
            Dbgl("Starting jump");
            jump.Start();
            return true;
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        public static class GameManager_Update_Patch
        {

            public static void Postfix(GameManager __instance, World ___m_World, GUIWindowManager ___windowManager)
            {
                if (!config.modEnabled || GameManager.Instance.isAnyCursorWindowOpen())
                    return;

                if (AedenthornUtils.CheckKeyDown(config.reloadKey))
                {
                    Dbgl($"Pressed reload key");
                    LoadConfig();
                }
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
