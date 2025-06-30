using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace KillNotification
{
    internal class KillChimeLoader : MonoBehaviour
    {
        public void Start()
        {
            StartCoroutine(LoadChimeCoroutine());

        }

        public IEnumerator LoadChimeCoroutine()
        {

            KillNotification.Dbgl($"Loading custom chime {Path.GetFileName(KillNotification.customChimePath)}");
            string url = string.Format("file://{0}", KillNotification.customChimePath);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    KillNotification.customChime = true;
                    KillNotification.killChime = DownloadHandlerAudioClip.GetContent(www);
                    KillNotification.Dbgl($"Loaded custom chime {KillNotification.customChimePath}");
                    Destroy(this);
                }
            }
        }
    }
}