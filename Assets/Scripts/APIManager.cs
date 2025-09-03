using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class APIManager : MonoBehaviour
{
    public int windowSize = 5; // how many seconds back to fetch
     string baseUrl = "http://localhost:5000/api";


    public IEnumerator FetchAllPlayerData()
    {
        // Step 1: Get current timestamp from heart_rate (or any single endpoint)
        string latestUrl = $"{baseUrl}/heart_rate";
        UnityWebRequest latestReq = UnityWebRequest.Get(latestUrl);
        yield return latestReq.SendWebRequest();

        if (latestReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Timestamp fetch error: {latestReq.error}");
            yield break;
        }

        HeartRateData latestHr = JsonConvert.DeserializeObject<HeartRateData>(latestReq.downloadHandler.text);
        long currentTsSec = latestHr.Timestamp_ms / 1000; // convert ms to seconds

        long startTs = currentTsSec - windowSize;
        long endTs = currentTsSec;

        Debug.Log($"Fetching window: {startTs} -> {endTs}");

        // Step 2: Fetch each type with dynamic timestamps
        yield return FetchData<HeartRateData>("heart_rate", startTs, endTs);
        yield return FetchData<GNSSData>("gnss", startTs, endTs);
        yield return FetchData<IMUData>("imu", startTs, endTs);
        yield return FetchData<ECGData>("ecg", startTs, endTs);
    }

    IEnumerator FetchData<T>(string dataType, long startTs, long endTs)
    {
        string url = $"{baseUrl}/{dataType}?start_timestamp={startTs}&end_timestamp={endTs}";
        UnityWebRequest www = UnityWebRequest.Get(url);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"{dataType} REST Error: {www.error}");
        }
        else
        {
            if (dataType == "heart_rate")
            {
                HeartRateData hr = JsonConvert.DeserializeObject<HeartRateData>(www.downloadHandler.text);
                Debug.Log($"{dataType}:\n" + JsonConvert.SerializeObject(hr, Formatting.Indented));
            }
            else if (dataType == "gnss")
            {
                GNSSData gnss = JsonConvert.DeserializeObject<GNSSData>(www.downloadHandler.text);
                Debug.Log($"{dataType}:\n" + JsonConvert.SerializeObject(gnss, Formatting.Indented));
            }
            else
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, List<T>>>(www.downloadHandler.text);
                if (json.ContainsKey("players"))
                {
                    foreach (var playerData in json["players"])
                    {
                        Debug.Log($"{dataType} player:\n" + JsonConvert.SerializeObject(playerData, Formatting.Indented));
                    }
                }
            }
        }
    }
}