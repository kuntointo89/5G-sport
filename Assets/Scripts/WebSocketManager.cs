using UnityEngine;
using NativeWebSocket;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

public class WebSocketManager : MonoBehaviour
{
    private WebSocket websocket;
    private Dictionary<string, PlayerController> playersDict = new Dictionary<string, PlayerController>();
    public float baseLat = 0f;
    public float baseLon = 0f;
    public ReplayManager replayManager;

    async void Start()
    {
        websocket = new WebSocket("ws://192.168.0.221:8765");

        websocket.OnOpen += () =>
        {
            Debug.Log("WebSocket connection opened!");
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("WebSocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket connection closed!");
        };

        websocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("[WebSocketManager] Message received: " + message);

            try
            {
                var packet = JsonConvert.DeserializeObject<PlayerPayload>(message);

                if (packet != null && !string.IsNullOrEmpty(packet.playerId))
                {
                    GameManager.Instance.UpdateOrMergePlayerPayload(packet);
                }
                else
                {

                    var wrapper = JsonConvert.DeserializeObject<PlayerMessageWrapper>(message);
                    if (wrapper?.players != null)
                        GameManager.Instance.HandlePlayerData(wrapper);
                    else
                        Debug.LogWarning("Unexpected message format: neither PlayerPayload nor PlayerMessageWrapper.");
        }
    }
    catch (JsonException ex)
    {
        Debug.LogError("Failed to parse WebSocket message: " + ex.Message);
    }
};
        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    /// <summary>
    /// Converts latitude and longitude to Unity world coordinates using a base origin.
    /// </summary>
    Vector2 LatLonToRinkCoords(float lat, float lon, float baseLat, float baseLon)
    {
        float metersY = (lat - baseLat) * 111320f;
        float metersX = (lon - baseLon) * (40075000f * Mathf.Cos(baseLat * Mathf.Deg2Rad) / 360f);
        return new Vector2(metersX, metersY);
    }
}
