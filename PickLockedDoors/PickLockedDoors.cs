using Audio;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;
using static GameEventManager;
using static Platform.XBL.MultiplayerActivityQueryManager;

namespace PickLockedDoors
{
    public class PickLockedDoors : IModApi
    {
        private static PickLockedDoors context;
        private static Mod mod;
        public static ModConfig config;
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
        [HarmonyPatch(typeof(TEFeatureLockPickable), nameof(TEFeatureLockPickable.InitBlockActivationCommands))]
        public static class TEFeatureLockPickable_InitBlockActivationCommands_Patch
        {
            public static bool Prefix(TEFeatureLockPickable __instance, Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData> _addCallback)
            {
                if (!config.modEnabled || __instance.lockPickSuccessEvent != "PickLockedDoor")
                    return true;

                var method = typeof(TEFeatureAbs).GetMethod(nameof(TEFeatureAbs.InitBlockActivationCommands));
                var ftn = method.MethodHandle.GetFunctionPointer();
                var func = (Action<Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData>>)Activator.CreateInstance(typeof(Action<Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData>>), __instance, ftn);
                func(_addCallback);
                _addCallback(new BlockActivationCommand("pick", "unlock", false, false, null), TileEntityComposite.EBlockCommandOrder.Last, __instance.FeatureData);
                return false;
            }
        }
        [HarmonyPatch(typeof(TEFeatureLockPickable), nameof(TEFeatureLockPickable.AllowBlockActivationCommand))]
        public static class TEFeatureLockPickable_AllowBlockActivationCommand_Patch
        {
            public static bool Prefix(TEFeatureLockPickable __instance, ReadOnlySpan<char> _commandName, ref bool __result)
            {
                if (!config.modEnabled || __instance.lockPickSuccessEvent != "PickLockedDoor" || __instance.Parent.GetFeature<TEFeatureLockable>()?.locked == true || !__instance.CommandIs(_commandName, "pick"))
                    return true;
                __instance.unlockCompletion = 1f;
                return false;
            }
        }
        [HarmonyPatch(typeof(Localization), nameof(Localization.Get))]
        public static class Localization_Get_Patch
        {
            public static void Prefix(ref string _key)
            {
                if (_key == "blockcommand_TEFeatureLockPickable:pick")
                    _key = "blockcommand_pick";
            }
        }
        [HarmonyPatch(typeof(TEFeatureLockPickable), nameof(TEFeatureLockPickable.EventData_Event))]
        public static class TEFeatureLockPickable_EventData_Event_Patch
        {
            public static void Postfix(TEFeatureLockPickable __instance)
            {
                if (__instance.lockPickSuccessEvent == "PickLockedDoor")
                {
                    Dbgl("Unlocking door");
                    __instance.Parent.GetFeature<TEFeatureLockable>().SetLocked(false);
                }
            }
        }

        [HarmonyPatch(typeof(TileEntityComposite), nameof(TileEntityComposite.read), new Type[] { typeof(PooledBinaryReader), typeof(TileEntity.StreamModeRead), typeof(int[]) })]
        static class TileEntityComposite_read_Patch
        {

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);

                Dbgl("Transpiling TileEntityComposite.read");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo mi && mi == AccessTools.Method(typeof(Log), nameof(Log.Warning), new Type[] { typeof(string) }))
                    {
                        Dbgl("Adding method to suppress warnings");
                        codes[i].operand = AccessTools.Method(typeof(PickLockedDoors), nameof(SuppressWarnings));
                    }
                }

                return codes.AsEnumerable();
            }
        }

        public static void SuppressWarnings(string warning)
        {
            if (config.modEnabled)
                return;
            Log.Warning(warning);
        }

        [HarmonyPatch(typeof(TileEntityCompositeData), new Type[] { typeof(BlockCompositeTileEntity), typeof(DynamicProperties) })]
        [HarmonyPatch(MethodType.Constructor)]
        public static class TileEntityCompositeData_Patch
        {
            public static void Prefix(BlockCompositeTileEntity _block, ref DynamicProperties _compositeProps)
            {
                if (!config.modEnabled)
                    return;
                int num = 0;
                bool door = false;
                bool flock = false;
                foreach (KeyValuePair<string, DynamicProperties> keyValuePair in _compositeProps.Classes)
                {
                    keyValuePair.Deconstruct(out var text, out var dynamicProperties);
                    if (!TileEntityCompositeData.knownFeatures.TryGetValue(text, out var type))
                    {
                        return;
                    }
                    if (type == typeof(TEFeatureLockPickable))
                        return;
                    if (type == typeof(TEFeatureDoor))
                        door = true;
                    if (type == typeof(TEFeatureLockable))
                        flock = true;
                    num++;
                }
                if (!door || !flock)
                    return;
                Dbgl($"Adding lockpick feature to {_block.GetBlockName()}");
                var props = new DynamicProperties();
                props.Values["LockPickTime"] = config.lockPickTime.ToString();
                props.Values["LockPickItem"] = config.lockPickItem;
                props.Values["LockPickBreakChance"] = config.lockPickBreakChance.ToString();
                props.Values["LockPickSuccessEvent"] = "PickLockedDoor";
                _compositeProps.Classes["TEFeatureLockPickable"] = props;
            }
        }
    }
}
