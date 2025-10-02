using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Path = System.IO.Path;

namespace VehicleRadio
{
    public class VehicleRadio : IModApi
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
        public static VehicleRadio context;
        public static Mod mod;

        public static List<RadioStation> stations = new List<RadioStation>();
        public static GameObject radioStationGo;
        public static List<Type> vehicleTypes = new List<Type>()
        {
            typeof(EntityVJeep),
            typeof(EntityMotorcycle),
            typeof(EntityVGyroCopter),
            typeof(EntityMinibike)
        };

        public void InitMod(Mod modInstance)
        {
            context = this;
            mod = modInstance;
            LoadConfig();

            Harmony harmony = new Harmony(GetType().ToString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            radioStationGo = new GameObject("RadioStationLoader");
            radioStationGo.AddComponent<RadioStationLoader>();
            GameObject.DontDestroyOnLoad(radioStationGo);
        }

        public static void LoadConfig()
        {
            var path = Path.Combine(mod.Path, "config.json");
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
            var path = Path.Combine(mod.Path, "config.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
        }

        public static void Dbgl(object str, bool prefix = true)
        {
            if(config.isDebug)
                Debug.Log((prefix ? mod.Name + " " : "") + str);
        }


        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
        static class GameManager_Update_Patch
        {

            static void Postfix(World ___m_World, GUIWindowManager ___windowManager)
            {   
                var entityPlayerLocal = ___m_World?.GetPrimaryPlayer();
                if (!config.modEnabled || entityPlayerLocal == null || GameManager.Instance.isAnyCursorWindowOpen(null) || entityPlayerLocal.AttachedToEntity == null || !vehicleTypes.Contains(entityPlayerLocal.AttachedToEntity.GetType()))
                    return;
                if (stations.Count == 0)
                    return;
                var rs = GetVehicleAudioSource(entityPlayerLocal.AttachedToEntity as EntityVehicle).GetComponent<RadioSwitch>();
                if (AedenthornUtils.CheckKeyDown(config.toggleRadio))
                {
                    if (rs.on)
                    {
                        rs.on = false;
                        GetVehicleAudioSource((EntityVehicle)entityPlayerLocal.AttachedToEntity).Stop();
                    }
                    else
                    {
                        rs.on = true;
                    }
                    Dbgl($"Pressed radio key; enabled: {rs.on}");
                    config.defaultOn = rs.on;
                    SaveConfig();
                    GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.toggleText, rs.on), string.Empty, null, null, false);
                }
                else if (AedenthornUtils.CheckKeyDown(config.nextStationKey))
                {
                    Dbgl($"Pressed next station key");
                    rs.station++;
                    if (rs.station >= stations.Count)
                    {
                        rs.station = 0;
                    }
                    config.defaultStation = rs.station;
                    GetVehicleAudioSource((EntityVehicle)entityPlayerLocal.AttachedToEntity).Stop();
                }
                else if (AedenthornUtils.CheckKeyDown(config.prevStationKey))
                {
                    Dbgl($"Pressed prev station key");
                    rs.station--;
                    if (rs.station < 0)
                    {
                        rs.station = stations.Count - 1;
                    }
                    SaveConfig();
                    GetVehicleAudioSource((EntityVehicle)entityPlayerLocal.AttachedToEntity).Stop();
                }
                else if (AedenthornUtils.CheckKeyDown(config.volumeUpKey))
                {
                    Dbgl($"Pressed volume up key");
                    rs.volume = Mathf.Clamp01(rs.volume + 0.1f);
                    SaveConfig();
                    GetVehicleAudioSource((EntityVehicle)entityPlayerLocal.AttachedToEntity).volume = rs.volume;
                    GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.volumeText, Mathf.RoundToInt(rs.volume * 100)), string.Empty, null, null, true);
                }
                else if (AedenthornUtils.CheckKeyDown(config.volumeDownKey))
                {
                    Dbgl($"Pressed volume up key");
                    rs.volume = Mathf.Clamp01(rs.volume - 0.1f);
                    SaveConfig();
                    GetVehicleAudioSource((EntityVehicle)entityPlayerLocal.AttachedToEntity).volume = rs.volume;
                    GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), string.Format(config.volumeText, Mathf.RoundToInt(rs.volume * 100)), string.Empty, null, null, true);
                }
            }
        }
        [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.Update))]
        static class EntityVehicle_Update_Patch
        {
            static void Postfix(EntityVehicle __instance)
            {
                if (!config.modEnabled || stations.Count == 0 || !__instance.LocalPlayerIsOwner() || (config.autoToggle && !__instance.HasDriver) || !vehicleTypes.Contains(__instance.GetType()))
                {
                    return;
                }
                var source = GetVehicleAudioSource(__instance);
                var rs = source.GetComponent<RadioSwitch>();

                if (rs.on && !source.isPlaying)
                {
                    RadioStation station = stations[Mathf.Clamp(rs.station, 0, stations.Count - 1)];
                    float offset =  (station.randomStart + Time.realtimeSinceStartup) % station.length;
                    foreach(var track in station.tracks)
                    {
                        if(offset <= track.length)
                        {
                            if(track.length - offset < 0.1f)
                            {
                                offset = 0;
                                continue;
                            }
                            source.clip = track;
                            source.time = offset;
                            source.Play();
                            Dbgl($"Starting radio {station.name} - {track.name} {offset} {station.randomStart + Time.realtimeSinceStartup} % {station.length}");

                            if(config.announceTrack)
                                GameManager.ShowTooltip(GameManager.Instance.World.GetPrimaryPlayer(), station.name + " - " + track.name, string.Empty, null, null, true);
                            break;
                        }
                        offset -= track.length;
                    }
                }
                else if(source.isPlaying && !rs.on)
                {
                    source.Stop();
                }
            }
        }
        public static AudioSource GetVehicleAudioSource(EntityVehicle vehicle)
        {
            
            var sourceTransform = vehicle.transform.Find("VehicleRadio");
            AudioSource source;
            if (sourceTransform == null)
            {
                Dbgl("no VehicleRadio transform");
                var go = new GameObject("VehicleRadio");
                sourceTransform = go.transform;
                sourceTransform.SetParent(vehicle.transform);
                sourceTransform.localPosition = Vector3.zero;
                source = go.AddComponent<AudioSource>();
                source.spatialize = true;
                source.dopplerLevel = 0;
                source.spatialBlend = 1.0f;
                var radioSwitch = go.AddComponent<RadioSwitch>();
                radioSwitch.on = config.defaultOn;
                radioSwitch.station = Mathf.Clamp(config.defaultStation, 0, stations.Count);
            }
            else
            {
                source = sourceTransform.GetComponent<AudioSource>();
            }
            
            return source;
        }
    }
}
