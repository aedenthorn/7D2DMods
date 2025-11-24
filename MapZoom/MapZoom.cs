using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Collections;
using UnityEngine;
using Path = System.IO.Path;

namespace MapZoom
{
    public class MapZoom : IModApi
    {

        public static ModConfig config;
        public static MapZoom context;
        public static Mod mod;
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        [HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.Update))]
        static class XUiC_MapArea_Update_Patch
        {
            public static void Prefix(XUiC_MapArea __instance, ref float __state)
            {
                if (!config.modEnabled || !config.isDebug)
                    return;
                __state = __instance.zoomScale;
            }
            public static void Postfix(XUiC_MapArea __instance, ref float __state)
            {
                if (!config.modEnabled || !config.isDebug || __state == __instance.zoomScale)
                    return;
                
            }
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiC_MapArea.Update");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.7f && codes[i + 1].opcode == OpCodes.Ldc_R4 && (float)codes[i + 1].operand == 6.15f)
                    {
                        Dbgl($"Overriding default zoom values");
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapZoom), nameof(MapZoom.OverrideMaxZoom))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapZoom), nameof(MapZoom.OverrideMinZoom))));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }

        }

        [HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.onMapScrolled))]
        static class XUiC_MapArea_onMapScrolled_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("Transpiling XUiC_MapArea.onMapScrolled");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 0.7f && codes[i + 2].opcode == OpCodes.Ldc_R4 && (float)codes[i + 2].operand == 6.15f)
                    {
                        Dbgl($"Overriding default zoom values");
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapZoom), nameof(MapZoom.OverrideMaxZoom))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapZoom), nameof(MapZoom.OverrideMinZoom))));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }

        }

        private static float OverrideMinZoom(float value)
        {
            if (!config.modEnabled)
                return value;
            return config.minZoom;
        }

        private static float OverrideMaxZoom(float value)
        {
            if (!config.modEnabled)
                return value;
            return config.maxZoom;
        }

        //[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.initMap))]
        static class XUiC_MapArea_initMap_Patch
        {

            public static void Postfix(XUiC_MapArea __instance)
            {
                if (!config.modEnabled)
                    return;
                __instance.xuiTexture.Size  *=  2;
            }
        }

        ////[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.updateMapSection))]
        //static class XUiC_MapArea_updateMapSection_Patch
        //{

        //    public static bool Prefix(XUiC_MapArea __instance, ref int mapStartX, ref int mapStartZ, ref int mapEndX, ref int mapEndZ, ref int drawnMapStartX, ref int drawnMapStartZ, ref int drawnMapEndX, ref int drawnMapEndZ)
        //    {
        //        if (!config.modEnabled)
        //            return true;
        //        int mult = Mathf.CeilToInt(__instance.zoomScale / 6.15f) - 1;
        //        if (mult < 1)
        //            return true;
        //        var dimen = mult * 2 + 1;
        //        __instance.mapTexture.Reinitialize(2048 * dimen, 2048 * dimen);
        //        __instance.xuiTexture.Size = new Vector2i(712, 712) * dimen;
        //        //Dbgl($"texture {__instance.mapTexture.width},{__instance.mapTexture.height}");
        //        IMapChunkDatabase mapDatabase = __instance.localPlayer.ChunkObserver.mapDatabase;
        //        NativeArray<Color32> rawTextureData = __instance.mapTexture.GetRawTextureData<Color32>();


        //        var sx = mapStartX - mult * 2048;
        //        var sz = mapStartZ - mult * 2048;
        //        var ex = mapEndX - mult * 2048;
        //        var ez = mapEndZ - mult * 2048;
        //        drawnMapStartX = 0;
        //        drawnMapStartZ = 0;
        //        drawnMapEndX = 2048;
        //        drawnMapEndZ = 2048;


        //        for (int w = 0; w < dimen; w++)
        //        {
        //            for (int h = 0; h < dimen; h++)
        //            {
        //                mapStartX = sx + w * 2048;
        //                mapStartZ = sz + h * 2048;
        //                mapEndX = ex + w * 2048;
        //                mapEndZ = ez + h * 2048;
        //                Dbgl($"{mapStartX},{mapEndX},{mapStartZ},{mapEndZ}");

        //                int i = mapStartZ;
        //                int num = drawnMapStartZ;
        //                while (i < mapEndZ)
        //                {
        //                    int j = mapStartX;
        //                    int num2 = drawnMapStartX;
        //                    while (j < mapEndX)
        //                    {
        //                        int num3 = World.toChunkXZ(j);
        //                        int num4 = World.toChunkXZ(i);

        //                        long num15 = WorldChunkCache.MakeChunkKey(num3, num4);
        //                        ushort[] mapColors = mapDatabase.GetMapColors(num15);
        //                        if (mapColors == null)
        //                        {
        //                            for (int l = 0; l < 256; l++)
        //                            {
        //                                int num16 = (num + l / 16) * 2048;
        //                                int num17 = num2 + l % 16 + num16;
        //                                rawTextureData[num17] = new Color32(0, 0, 0, 0);
        //                            }
        //                        }
        //                        else
        //                        {
        //                            bool flag2 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3, num4 + 1));
        //                            bool flag3 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3, num4 - 1));
        //                            bool flag4 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3 - 1, num4));
        //                            bool flag5 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3 + 1, num4));
        //                            int num18 = 0;
        //                            if (flag2 && flag3 && flag4 && flag5)
        //                            {
        //                                bool flag6 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3 - 1, num4 + 1));
        //                                bool flag7 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3 + 1, num4 + 1));
        //                                bool flag8 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3 - 1, num4 - 1));
        //                                bool flag9 = mapDatabase.Contains(WorldChunkCache.MakeChunkKey(num3 + 1, num4 - 1));
        //                                if (!flag6)
        //                                {
        //                                    num18 = 9;
        //                                }
        //                                else if (!flag7)
        //                                {
        //                                    num18 = 10;
        //                                }
        //                                else if (!flag9)
        //                                {
        //                                    num18 = 11;
        //                                }
        //                                else if (!flag8)
        //                                {
        //                                    num18 = 12;
        //                                }
        //                                else
        //                                {
        //                                    num18 = 4;
        //                                }
        //                            }
        //                            else
        //                            {
        //                                if (flag3 && !flag2)
        //                                {
        //                                    num18 += 6;
        //                                }
        //                                else if (flag3 && flag2)
        //                                {
        //                                    num18 += 3;
        //                                }
        //                                if (flag5 && flag4)
        //                                {
        //                                    num18++;
        //                                }
        //                                else if (flag4)
        //                                {
        //                                    num18 += 2;
        //                                }
        //                            }
        //                            byte[] array = __instance.fowChunkMaskAlphas[num18];
        //                            if (!__instance.bFowMaskEnabled)
        //                            {
        //                                array = __instance.fowChunkMaskAlphas[4];
        //                            }
        //                            for (int m = 0; m < 256; m++)
        //                            {
        //                                int num19 = m / 16;
        //                                int num20 = m % 16;
        //                                int num21 = (num + num19) * 2048;
        //                                int num22 = num2 + num20;
        //                                int num23 = num21 + num22;
        //                                int num24 = num19 * 16;

        //                                byte b = array[num24 + num20];
        //                                Color32 color2 = Utils.FromColor5To32(mapColors[m]);

        //                                rawTextureData[num23] = new Color32(color2.r, color2.g, color2.b, (b < byte.MaxValue) ? b : byte.MaxValue);
        //                            }
        //                        }
        //                        j += 16;
        //                        num2 = Utils.WrapIndex(num2 + 16, 2048);
        //                    }
        //                    i += 16;
        //                    num = Utils.WrapIndex(num + 16, 2048);
        //                }
        //            }
        //        }

        //        return false;
        //    }
        //}

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
