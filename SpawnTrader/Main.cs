using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace SpawnTrader
{
    public class Main : IModApi
    {
        public static List<Vector3i> sortedStorageList = new List<Vector3i>();
        public static Vector3i currentStorage;
        public static Dictionary<string, string> nameDict = new Dictionary<string, string>();
        public static bool showingList;
        public static string nameDictPath;
        public static float elapsedTime;
        public static bool editing;
        public static Rect windowRect;

        public static ModConfig config;
        public static Main context;
        public static Mod mod;

        public static List<string> traderNames = new List<string>()
        {
            "npcTraderJoel",
            "npcTraderRekt",
            "npcTraderBob",
            "npcTraderHugh",
            "npcTraderJen"
        };
        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            //windowRect = new Rect(config.windowPositionX, config.windowPositionY, config.buttonWidth + config.buttonHeight + 40, config.windowHeight);
            //GameObject go = new GameObject("RemoteStorageGUI");
            //Object.DontDestroyOnLoad(go);
            //go.AddComponent<RemoteStorageGUI>();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());

        }

        public static void LoadConfig()
        {
            var path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), "config.json");
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
            var path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.ModInfo.Name.Value + " " : "") + str);
        }

        //[HarmonyPatch(typeof(GameManager), "StartGame")]
        static class GameManager_StartGame_Patch
        {
            static void Prefix()
            {
                nameDictPath = null;
            }
        }
        [HarmonyPatch(typeof(GameManager), "Update")]
        static class GameManager_Update_Patch
        {

            static void Postfix(World ___m_World, GUIWindowManager ___windowManager)
            {
                var entityPlayerLocal = ___m_World?.GetPrimaryPlayer();
                if (!config.modEnabled || entityPlayerLocal == null || entityPlayerLocal.PlayerUI.windowManager.IsModalWindowOpen())
                    return;

                if (AedenthornUtils.CheckKeyDown(config.spawnTraderKey))
                {
                    Dbgl($"Pressed spawn key");

                    EntityTrader trader = GetTargetedTrader(entityPlayerLocal);
                    if (trader != null)
                    {
                        ___m_World.RemoveEntity(trader.entityId, EnumRemoveEntityReason.Undef);
                        return;
                    }
                    else
                    {
                        EntityTrader entity = (EntityTrader)EntityFactory.CreateEntity(EntityClass.FromString(config.traderToSpawn), GameManager.Instance.World.GetPrimaryPlayer().position + GameManager.Instance.World.GetPrimaryPlayer().GetForwardVector(), GameManager.Instance.World.GetPrimaryPlayer().rotation + new Vector3(0, 180, 0));
                        GameManager.Instance.World.SpawnEntityInWorld(entity);
                    }
                }
                else if (AedenthornUtils.CheckKeyDown(config.nextTraderKey))
                {
                    Dbgl($"Pressed next trader key");
                    EntityTrader trader = GetTargetedTrader(entityPlayerLocal);
                    if (trader != null)
                    {
                        int index;
                        for(index = 0; index < traderNames.Count; index++)
                        {
                            if(EntityClass.FromString(traderNames[index]) == trader.entityClass)
                                break;
                        }
                        string oldName = traderNames[index];
                        index++;
                        index %= traderNames.Count;
                        Dbgl($"Spawning {traderNames[index]} in place of {oldName}");
                        ___m_World.SpawnEntityInWorld(EntityFactory.CreateEntity(EntityClass.FromString(traderNames[index]), trader.position, trader.rotation));
                        ___m_World.RemoveEntity(trader.entityId, EnumRemoveEntityReason.Undef);
                    }
                    else
                    {
                        int index = traderNames.IndexOf(config.traderToSpawn);
                        index++;
                        index %= traderNames.Count;
                        Dbgl($"Switching to {traderNames[index]}");
                        config.traderToSpawn = traderNames[index];
                        SaveConfig();
                    }
                }
                else if (AedenthornUtils.CheckKeyDown(config.prevTraderKey))
                {
                    Dbgl($"Pressed prev trader key");
                    EntityTrader trader = GetTargetedTrader(entityPlayerLocal);
                    if (trader != null)
                    {
                        int index;
                        for(index = 0; index < traderNames.Count; index++)
                        {
                            if(EntityClass.FromString(traderNames[index]) == trader.entityClass)
                                break;
                        }
                        string oldName = traderNames[index];
                        index--;
                        if (index < 0)
                            index = traderNames.Count - 1;
                        Dbgl($"Spawning {traderNames[index]} in place of {oldName}");
                        ___m_World.SpawnEntityInWorld(EntityFactory.CreateEntity(EntityClass.FromString(traderNames[index]), trader.position, trader.rotation));
                        ___m_World.RemoveEntity(trader.entityId, EnumRemoveEntityReason.Undef);
                    }
                    else
                    {
                        int index = traderNames.IndexOf(config.traderToSpawn);
                        index--;
                        if (index < 0)
                            index = traderNames.Count - 1;
                        Dbgl($"Switching to {traderNames[index]}");
                        config.traderToSpawn = traderNames[index];
                        SaveConfig();
                    }
                }
            }

            private static EntityTrader GetTargetedTrader(EntityPlayerLocal entityPlayerLocal)
            {
                Ray ray = entityPlayerLocal.GetLookRay();
                ray.origin += ray.direction.normalized * 0.1f;
                RaycastHit raycastHit;
                float dist = Utils.FastMax(Utils.FastMax(Constants.cDigAndBuildDistance, Constants.cCollectItemDistance), 30f);

                bool hit = Physics.Raycast(new Ray(ray.origin - Origin.position, ray.direction), out raycastHit, dist);

                if (!hit || raycastHit.transform is null)
                    return null;

                Transform t = raycastHit.transform;
                Entity entity;
                while (t != null)
                {
                    if ((entity = t.GetComponent<Entity>()) != null)
                    {
                        if (entity is EntityTrader)
                        {
                            Dbgl($"Returning {entity.name}");
                            return entity as EntityTrader;
                        }
                        Dbgl($"Wrong entity, returning null");
                        return null;
                    }
                    t = t.parent;
                }
                Dbgl($"Returning null");
                return null;
            }
        }
    }
}
