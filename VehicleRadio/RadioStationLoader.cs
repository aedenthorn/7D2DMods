using DynamicMusic;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace VehicleRadio
{
    public class RadioStationLoader : MonoBehaviour
    {
        
        public void Awake()
        {
            StartCoroutine(LoadSongCoroutine());

        }

        public IEnumerator LoadSongCoroutine()
        {
            VehicleRadio.stations.Clear();
            foreach (var dir in Directory.GetDirectories(Path.Combine(VehicleRadio.mod.Path, "Stations"), "*", SearchOption.AllDirectories))
            {
                VehicleRadio.Dbgl($"Got station {Path.GetFileName(dir)}");

                RadioStation station = new RadioStation();
                station.name = Path.GetFileName(dir);
                station.tracks = new List<AudioClip>();
                foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    string url = string.Format("file://{0}", file);
                    using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN))
                    {
                        yield return www.SendWebRequest();

                        if (www.result == UnityWebRequest.Result.Success)
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                            if (clip != null)
                            {
                                clip.name = Path.GetFileNameWithoutExtension(file);
                                station.tracks.Add(clip);
                                station.length += clip.length;
                            }

                        }
                    }
                }
                if(station.tracks.Count > 0)
                {
                    station.randomStart = Random.Range(0, station.length);
                    VehicleRadio.Dbgl($"Got {station.tracks.Count} tracks; {(int)station.length}s");
                    VehicleRadio.stations.Add(station);
                }
            }
        }

    }
}