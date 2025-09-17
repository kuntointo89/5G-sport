using UnityEngine;
using NativeWebSocket;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class WebSocketManager : MonoBehaviour
{
    private WebSocket websocket;

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () =>
        {
            Debug.Log(" WebSocket connection opened!");
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError(" WebSocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log(" WebSocket connection closed!");
        };

        websocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log($"[WebSocketManager] Message received: {message}");

            try
            {
                JObject sensorData = JObject.Parse(message);
                string topic = sensorData["Topic"]?.ToString();

                if (string.IsNullOrEmpty(topic))
                {
                    Debug.LogWarning(" Received message without Topic field.");
                    return;
                }

                var packet = new PlayerPayload();
                packet.playerId = sensorData["Pico_ID"]?.ToString();

                switch (topic)
                {
                    case "sensors.gnss":
                        packet.type = "GNSS";
                        packet.GNSS = JsonConvert.DeserializeObject<GNSSData>(message);
                        break;

                    case "sensors.ecg":
                        packet.type = "ECG";
                        packet.ECG = JsonConvert.DeserializeObject<ECGData>(message);
                        break;

                    case "sensors.hr":
                        packet.type = "HEARTRATE";
                        packet.Heart_rate = JsonConvert.DeserializeObject<HeartRateData>(message);
                        break;

                    case "sensors.imu":
                        packet.type = "IMU";
                        packet.IMU = JsonConvert.DeserializeObject<IMUData>(message);
                        break;

                    default:
                        Debug.LogWarning($" Unknown topic type: {topic}");
                        return;
                }

                if (!string.IsNullOrEmpty(packet.playerId))
                {
                    GameManager.Instance.UpdateOrMergePlayerPayload(packet);
                }
                else
                {
                    Debug.LogWarning(" Packet missing playerId, ignoring.");
                }
            }
            catch (JsonException ex)
            {
                Debug.LogError(" Failed to parse WebSocket message: " + ex.Message);
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
}
