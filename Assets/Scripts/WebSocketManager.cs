using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public class WebSocketManager : MonoBehaviour
{
    WebSocket websocket;
    Dictionary<string, PlayerController> playersDict = new Dictionary<string, PlayerController>();

    public float baseLat = 0f;
    public float baseLon = 0f;

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () =>
        {
            Debug.Log("Connection open!");
        };

        websocket.OnError += (e) =>
        {
            Debug.Log("Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
        };

        websocket.OnMessage += (bytes) =>
    {
        string message = Encoding.UTF8.GetString(bytes);
        Debug.Log("Message received: " + message);

        var json = JObject.Parse(message);
        var playerArray = json["players"];

        foreach (var player in playerArray)
    {

    string playerId = player["playerId"]?.ToString();
    float lat = player["latitude"]?.ToObject<float>() ?? 0f;
    float lon = player["longitude"]?.ToObject<float>() ?? 0f;

    Vector2 worldPos = LatLonToRinkCoords(lat, lon, baseLat, baseLon);

    if (playersDict.ContainsKey(playerId))
    {
        playersDict[playerId].UpdatePosition(worldPos);

        var ecgArrayRaw = player["ecgSample"];
        Debug.Log($"Raw ECG array for {playerId}: {ecgArrayRaw}");

        var ecgArray = ecgArrayRaw as JArray;
        if (ecgArray != null && ecgArray.Count > 0)
        {
            int latestEcgSample = ecgArray[ecgArray.Count - 1].ToObject<int>();
            playersDict[playerId].ReceiveECGSample(latestEcgSample);
            Debug.Log($"Received ECG sample for {playerId}: {latestEcgSample}");
        }
    }
    else
    {
        Debug.LogWarning($"Player not found in dictionary: {playerId}");
        }
    }
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue(); // Required for NativeWebSocket
#endif
    }

    private void OnApplicationQuit()
    {
        websocket.Close();
    }

    Vector2 LatLonToRinkCoords(float lat, float lon, float baseLat, float baseLon)
    {
        float metersY = (lat - baseLat) * 111320f;
        float metersX = (lon - baseLon) * (40075000f * Mathf.Cos(baseLat * Mathf.Deg2Rad) / 360f);
        return new Vector2(metersX, metersY);
    }
}
