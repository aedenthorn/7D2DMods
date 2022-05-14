using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ReadablePOINames
{
    public class ReadablePOINames : IModApi
    {
        private static ReadablePOINames context;
        private static Mod mod;
        public static ModConfig config;
        public static Dictionary<string, List<string>> parts = new Dictionary<string, List<string>>()
        {
            { 
                "one", new List<string>()
                {
                    "old", 
                } 
            },
            { 
                "two", new List<string>()
                {
                    "ranch",
                    "rural",
                    "mansard",
                    "downtown",
                }
            },
            { 
                "three", new List<string>()
                {
                    "store",
                    "housing",
                    "house",
                }
            },
            { 
                "four", new List<string>()
                {
                    "development",
                    "strip",
                    "filler"
                }
            },
            { 
                "five", new List<string>()
                {
                    "plaza",
                    "remnant",
                    "rubble"
                }
            },
        };
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
        [HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.SetActiveQuests))]
        static class EntityTrader_SetActiveQuests_Patch
        {

            static void Postfix(EntityTrader __instance)
            {
                if (!config.modEnabled)
                    return;
                for(int i = 0; i < __instance.activeQuests.Count; i++)
                {
                    if(__instance.activeQuests[i].DataVariables.TryGetValue("POIName", out string poi_key))
                    {
                        __instance.activeQuests[i].DataVariables["POIName"] = GetPOIName(poi_key);
                    }
                }
            }

            private static string GetPOIName(string poi_key)
            {
                var parts = poi_key.Split('_');
                
            }
        }
    }
}
