using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReplayManager : MonoBehaviour
{
    private List<PlayerAPIResponse> recordedFrames = new();
    private Queue<PlayerAPIResponse> bufferedFrames = new();
    private Dictionary<string, PlayerController> replayPlayers = new();

    public bool IsShowingReplay { get; private set; } = false;

    [Header("Replay Settings")]
    public GameObject playerPrefabReplay; // Assign in Inspector

    private Action onReplayComplete;

    private void Awake()
    {
        Debug.Log("ReplayManager Awake");
    }

    #region Recording

    public void RecordFrame(PlayerAPIResponse frame)
    {
        if (IsShowingReplay)
        {
            bufferedFrames.Enqueue(frame);
            return;
        }

        recordedFrames.Add(frame);
        ApplyLiveFrame(frame);
    }

    private void ApplyLiveFrame(PlayerAPIResponse frame)
    {
        var payload = ConvertFrameToPayload(frame);
        GameManager.Instance.ProcessPlayerPayload(payload);
    }

    #endregion

    #region Replay

    public void ReplayRecording()
    {
        if (recordedFrames.Count == 0)
        {
            Debug.LogWarning("ReplayManager: No frames recorded yet.");
            return;
        }

        PlayReplay(new List<PlayerAPIResponse>(recordedFrames), () =>
        {
            Debug.Log("Replay complete!");
        });
    }

    public void PlayReplay(List<PlayerAPIResponse> frames, Action onComplete)
    {
        if (frames == null || frames.Count == 0)
        {
            Debug.LogWarning("ReplayManager: No frames to replay.");
            onComplete?.Invoke();
            return;
        }

        IsShowingReplay = true;
        onReplayComplete = onComplete;

        HideLivePlayers();
        StartCoroutine(ReplayCoroutine(frames));
    }

    private IEnumerator ReplayCoroutine(List<PlayerAPIResponse> frames)
    {
        ClearReplayPlayers();

        // Sort frames by timestamp (if your timestamp is numeric)
        frames.Sort((a, b) =>
        {
            if (long.TryParse(a.timestamp, out long ta) && long.TryParse(b.timestamp, out long tb))
                return ta.CompareTo(tb);
            return 0;
        });

        for (int i = 0; i < frames.Count; i++)
        {
            var currentFrame = frames[i];
            ApplyReplayFrame(currentFrame);

            // Calculate delay to next frame based on timestamp
            if (i < frames.Count - 1 &&
                long.TryParse(currentFrame.timestamp, out long t1) &&
                long.TryParse(frames[i + 1].timestamp, out long t2))
            {
                float delay = (t2 - t1) / 1000f; // assuming timestamp in ms
                yield return new WaitForSeconds(Mathf.Max(delay, 0.01f)); // avoid zero or negative
            }
            else
            {
                yield return null; // fallback
            }
        }

        EndReplay();
    }

    private void ApplyReplayFrame(PlayerAPIResponse frame)
    {
        if (frame == null || string.IsNullOrEmpty(frame.playerId)) return;

        if (!replayPlayers.TryGetValue(frame.playerId, out var player) || player == null)
        {
            Vector2 startPos = Vector2.zero;
            if (frame.GNSS != null)
            {
                startPos = GPSUtils.GPSToMeters(
                    frame.GNSS.Latitude,
                    frame.GNSS.Longitude,
                    GameManager.Instance.baseLat,
                    GameManager.Instance.baseLon
                );

                startPos = new Vector2(
                    Mathf.Clamp(startPos.x, GameManager.Instance.rinkMin.x + 1, GameManager.Instance.rinkMax.x - 1),
                    Mathf.Clamp(startPos.y, GameManager.Instance.rinkMin.y + 1, GameManager.Instance.rinkMax.y - 1)
                );
            }

            var obj = Instantiate(playerPrefabReplay, new Vector3(startPos.x, startPos.y, 0), Quaternion.identity);
            player = obj.GetComponent<PlayerController>();
            player.player_name = frame.playerId;
            replayPlayers[frame.playerId] = player;
        }

        // Update position
        if (frame.GNSS != null)
        {
            Vector2 meters = GPSUtils.GPSToMeters(
                frame.GNSS.Latitude,
                frame.GNSS.Longitude,
                GameManager.Instance.baseLat,
                GameManager.Instance.baseLon
            );

            Vector2 clamped = new(
                Mathf.Clamp(meters.x, GameManager.Instance.rinkMin.x + 1, GameManager.Instance.rinkMax.x - 1),
                Mathf.Clamp(meters.y, GameManager.Instance.rinkMin.y + 1, GameManager.Instance.rinkMax.y - 1)
            );

            player.SetTargetPosition(clamped);
        }

        // Heart rate
        if (frame.Heart_rate != null)
            player.UpdateHeartRate((int)frame.Heart_rate.Average_BPM);

        // IMU
        if (frame.IMU != null)
            player.UpdateIMU(frame.IMU);

        // ECG
        if (frame.ECG?.Samples != null && frame.ECG.Samples.Count > 0)
            player.UpdateECGFrame(frame.ECG.Samples);
    }

    private void EndReplay()
    {
        ClearReplayPlayers();
        ShowLivePlayers();

        while (bufferedFrames.Count > 0)
            ApplyLiveFrame(bufferedFrames.Dequeue());

        IsShowingReplay = false;
        onReplayComplete?.Invoke();
    }

    #endregion

    #region Helpers

    private void HideLivePlayers()
    {
        foreach (var livePlayer in GameManager.Instance.playerControllers.Values)
            livePlayer.gameObject.SetActive(false);
    }

    private void ShowLivePlayers()
    {
        foreach (var livePlayer in GameManager.Instance.playerControllers.Values)
            livePlayer.gameObject.SetActive(true);
    }

    private void ClearReplayPlayers()
    {
        foreach (var rp in replayPlayers.Values)
        {
            if (rp != null) Destroy(rp.gameObject);
        }
        replayPlayers.Clear();
    }

    private PlayerPayload ConvertFrameToPayload(PlayerAPIResponse frame)
    {
        return new PlayerPayload
        {
            playerId = frame.playerId,
            GNSS = frame.GNSS ?? new GNSSData(),
            Heart_rate = frame.Heart_rate ?? new HeartRateData(),
            IMU = frame.IMU ?? new IMUData(),
            ECG = frame.ECG ?? new ECGData()
        };
    }

    #endregion
}
