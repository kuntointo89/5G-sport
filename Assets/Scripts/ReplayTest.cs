using System.Collections.Generic;
using UnityEngine;
using System;

public class ReplayTest : MonoBehaviour
{
    public ReplayManager replayManager;

    double unixTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();

    private void Start()
    {
        // Simulate 3 frames for one player
        List<PlayerAPIResponse> frames = new List<PlayerAPIResponse>();

        for (int i = 0; i < 3; i++)
        {
            frames.Add(new PlayerAPIResponse
            {
                playerId = "Player_1",
                GNSS = new GNSSData
                {
                    Latitude = 37.7749 + i * 0.0001f,
                    Longitude = -122.4194 + i * 0.0001f,
                },
                Heart_rate = new HeartRateData { average_bpm = 70 + i * 5 },
                IMU = new IMUData { /* fill with test values */ },
                ECG = new ECGData { Samples = new List<int> { 100, 200, 300 } },
                timestamp = (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i * 1000).ToString()
            });
        }

        // Send frames to ReplayManager
        replayManager.PlayReplay(frames, () =>
        {
            Debug.Log("Test Replay Finished");
        });
    }
}