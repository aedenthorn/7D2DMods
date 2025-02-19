using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WorldReadyChime
{
    internal class WorldChimeLoader : MonoBehaviour
    {
        public void Start()
        {
            StartCoroutine(LoadChimeCoroutine());

        }

        public IEnumerator LoadChimeCoroutine()
        {

            WorldReadyChime.Dbgl($"Loading custom chime {Path.GetFileName(WorldReadyChime.customChimePath)}");
            string url = string.Format("file://{0}", WorldReadyChime.customChimePath);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    WorldReadyChime.customChime = true;
                    WorldReadyChime.spawnChime = DownloadHandlerAudioClip.GetContent(www);
                    WorldReadyChime.Dbgl($"Loaded custom chime {WorldReadyChime.customChimePath}");
                }
            }
        }
    }
}