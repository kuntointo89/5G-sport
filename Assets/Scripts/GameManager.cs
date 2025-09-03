using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using TMPro;
using SimpleJSON;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    [Header("Player Properties")]
    public string player_name;
    public GameObject statsPanel;
    public TMP_Text statsText;

    private Vector2 targetPosition;

    [Header("Rink Corners")]
    public float cornerRadius = 2.5f;  // Radius to define rounded corners of rink bounds

    [Header("Safe Spawn Area")]
    public float spawnMargin = 3f; // Margin inside rink edges to restrict player/puck spawn area

    [Header("Player List UI")]
    public TMP_Dropdown playerDropdown;  // Dropdown UI to select players
    public Transform playerListParent;   // Parent transform for player UI elements (not used in code)

    [Header("Prefabs & UI")]
    public GameObject playerPrefab;      // Player GameObject prefab to instantiate
    public GameObject statsPanelPrefab;  // Stats panel prefab to show player info on screen

    [Header("Rink Bounds (in meters)")]
    public Vector2 rinkMin = new Vector2(-30, -15); // Bottom-left corner of rink area in meters
    public Vector2 rinkMax = new Vector2(30, 15);   // Top-right corner of rink area in meters

    [Header("Puck Reference")]
    public PuckMovement puckController;  // Reference to puck controller script

    private List<string> playerIds = new();
    private List<PlayerPayload> initialPlayersBuffer = new();
    private Dictionary<string, PlayerPayload> partialPayloads = new Dictionary<string, PlayerPayload>();

    private const int initialPlayersCount = 5; // Number of players required to set GPS origin

    public Dictionary<string, PlayerController> playerControllers = new();
    private Dictionary<string, GameObject> statsPanels = new();

    private WebSocket websocket;
    private Canvas screenCanvas;

    private HashSet<string> activePlayersThisFrame = new HashSet<string>();

    public double baseLat = 0;
    public double baseLon = 0;
    private Vector2 gpsOriginMeters = Vector2.zero;
    private bool gpsOriginSet = false;

    public APIManager apiManager;
    public ReplayManager replayManager;

    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (screenCanvas == null)
        {
            screenCanvas = FindFirstObjectByType<Canvas>();
            if (screenCanvas == null)
                Debug.LogError("No Canvas found in the scene! Please add one.");
        }
    }

    // Called from WebSocketManager when new player data arrives
    public void HandlePlayerData(PlayerMessageWrapper wrapper)
    {
        if (replayManager != null && replayManager.IsShowingReplay)
            return;

        activePlayersThisFrame.Clear();

        foreach (var player in wrapper.players)
        {
            if (player.GNSS == null)
                continue;

            string normalizedId = NormalizePlayerId(player.playerId);
            activePlayersThisFrame.Add(normalizedId);

            if (!gpsOriginSet)
            {
                initialPlayersBuffer.Add(player);

                if (initialPlayersBuffer.Count >= initialPlayersCount)
                {
                    baseLat = 0;
                    baseLon = 0;
                    foreach (var p in initialPlayersBuffer)
                    {
                        baseLat += p.GNSS.Latitude;
                        baseLon += p.GNSS.Longitude;
                    }

                    baseLat /= initialPlayersBuffer.Count;
                    baseLon /= initialPlayersBuffer.Count;
                    gpsOriginSet = true;

                    foreach (var p in initialPlayersBuffer)
                        ProcessPlayerPayload(p);

                    initialPlayersBuffer.Clear();
                }

                continue;
            }

            ProcessPlayerPayload(player);
        }

        // Remove players not active this frame
        var allIds = new List<string>(playerControllers.Keys);
        foreach (var id in allIds)
        {
            if (!activePlayersThisFrame.Contains(id))
                RemovePlayer(id);
        }
    }

    public void ProcessPlayerPayload(PlayerPayload payload)
    {
        string normalizedId = NormalizePlayerId(payload.playerId);
        if (payload.GNSS == null)
        {
            Debug.LogWarning("[GameManager] ProcessPlayerPayload called without GNSS");
            return;
        }

        if (!playerControllers.TryGetValue(normalizedId, out var pc))
        {
            GameObject playerObj = InstantiatePlayerGameObject(normalizedId);
            pc = playerObj.GetComponent<PlayerController>();

            if (pc == null)
            {
                Debug.LogError("[GameManager] PlayerController missing on prefab!");
                return;
            }

            playerControllers[normalizedId] = pc;
        }

        Vector2 meters = GPSUtils.GPSToMeters(payload.GNSS.Latitude, payload.GNSS.Longitude, baseLat, baseLon);

        Vector2 clamped = new Vector2(
            Mathf.Clamp(meters.x, rinkMin.x + spawnMargin, rinkMax.x - spawnMargin),
            Mathf.Clamp(meters.y, rinkMin.y + spawnMargin, rinkMax.y - spawnMargin)
        );

        clamped = AdjustPositionAvoidingCorners(clamped);

        pc.UpdatePosition(clamped);

        if (payload.IMU != null) pc.UpdateIMU(payload.IMU);
        if (payload.Heart_rate != null) pc.UpdateHeartRate((int)payload.Heart_rate.average_bpm);
        if (payload.ECG != null && payload.ECG.Samples != null && payload.ECG.Samples.Count > 0)
            pc.ReceiveECGSample(payload.ECG.Samples[^1]);

    }

    private GameObject InstantiatePlayerGameObject(string normalizedId)
{
    if (!playerPrefab || !statsPanelPrefab || !screenCanvas)
    {
        Debug.LogError("Missing prefab or canvas!");
        return null;
    }

    // Spawn position
    Vector3 spawnPos = Vector3.zero;

    // Instantiate player and stats panel
    var playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
    var statsPanel = Instantiate(statsPanelPrefab, screenCanvas.transform);

    // Ensure the stats panel is active and visible
    statsPanel.SetActive(true);

    // Get PlayerController
    var pc = playerObj.GetComponent<PlayerController>();
    if (!pc)
    {
        Debug.LogError("Player prefab missing PlayerController");
        Destroy(playerObj);
        Destroy(statsPanel);
        return null;
    }

    // Assign references
    pc.player_name = normalizedId;
    pc.statsPanel = statsPanel;
    pc.statsText = statsPanel.GetComponentInChildren<TMPro.TMP_Text>();
    pc.rinkMin = rinkMin;
    pc.rinkMax = rinkMax;
    pc.InitializeRealtime();

    // Store the panel for later dropdown control
    statsPanels[normalizedId] = statsPanel;

    // Add player to dropdown and update selection
    AddPlayerToDropdown(normalizedId);

    // Show this player's panel and hide others
    OnPlayerDropdownChanged(playerDropdown.value);

    return playerObj;
}


    public void UpdateSinglePlayer(PlayerPayload packet)
    {
        if (packet == null || string.IsNullOrEmpty(packet.playerId))
        {
            Debug.LogWarning("[GameManager] UpdateSinglePlayer: null or invalid packet");
            return;
        }

        string normalizedId = NormalizePlayerId(packet.playerId);

        if (!playerControllers.TryGetValue(normalizedId, out var pc))
        {
            if (packet.GNSS == null)
            {
                Debug.Log($"[GameManager] Deferring spawn: no GNSS yet for {normalizedId}");
                return;
            }
            else
            {
                ProcessPlayerPayload(packet);
                if (!playerControllers.TryGetValue(normalizedId, out pc))
                {
                    Debug.LogWarning($"[GameManager] Failed to spawn player {normalizedId}");
                    return;
                }
            }
        }

        if (packet.GNSS != null)
        {
            Vector2 meters = GPSUtils.GPSToMeters(packet.GNSS.Latitude, packet.GNSS.Longitude, baseLat, baseLon);

            Vector2 clamped = new Vector2(
                Mathf.Clamp(meters.x, rinkMin.x + spawnMargin, rinkMax.x - spawnMargin),
                Mathf.Clamp(meters.y, rinkMin.y + spawnMargin, rinkMax.y - spawnMargin)
            );

            clamped = AdjustPositionAvoidingCorners(clamped);

            pc.UpdatePosition(clamped);
        }

        if (packet.IMU != null)
        {
            pc.UpdateIMU(packet.IMU);
        }

        if (packet.Heart_rate != null)
        {
            pc.UpdateHeartRate((int)packet.Heart_rate.average_bpm);
        }

        if (packet.ECG != null && packet.ECG.Samples != null && packet.ECG.Samples.Count > 0)
        {
            pc.ReceiveECGSample(packet.ECG.Samples[^1]);
        }

        Debug.Log($"[GameManager] Updated player {normalizedId} (GNSS={packet.GNSS != null}, IMU={packet.IMU != null}, HR={packet.Heart_rate != null}, ECG={packet.ECG != null})");
    }

    public void UpdateOrMergePlayerPayload(PlayerPayload incoming)
    {
        if (incoming == null || string.IsNullOrEmpty(incoming.playerId))
        {
            Debug.LogWarning("[GameManager] UpdateOrMergePlayerPayload called with null/invalid payload.");
            return;
        }

        string normalizedId = NormalizePlayerId(incoming.playerId);

        if (!partialPayloads.TryGetValue(normalizedId, out var merged))
        {
            merged = new PlayerPayload { playerId = normalizedId };
            partialPayloads[normalizedId] = merged;
        }

        if (!string.IsNullOrEmpty(incoming.type))
        {
            string t = incoming.type.ToUpperInvariant();
            switch (t)
            {
                case "GNSS":
                    merged.GNSS = incoming.GNSS;
                    break;
                case "IMU":
                    merged.IMU = incoming.IMU;
                    break;
                case "HR":
                case "HEARTRATE":
                    merged.Heart_rate = incoming.Heart_rate;
                    break;
                case "ECG":
                    merged.ECG = incoming.ECG;
                    break;
                default:
                    if (incoming.GNSS != null) merged.GNSS = incoming.GNSS;
                    if (incoming.IMU != null) merged.IMU = incoming.IMU;
                    if (incoming.Heart_rate != null) merged.Heart_rate = incoming.Heart_rate;
                    if (incoming.ECG != null) merged.ECG = incoming.ECG;
                    break;
            }
        }
        else
        {
            if (incoming.GNSS != null) merged.GNSS = incoming.GNSS;
            if (incoming.IMU != null) merged.IMU = incoming.IMU;
            if (incoming.Heart_rate != null) merged.Heart_rate = incoming.Heart_rate;
            if (incoming.ECG != null) merged.ECG = incoming.ECG;
        }

        if (merged.GNSS != null && !playerControllers.ContainsKey(normalizedId))
        {
            Debug.Log($"[GameManager] Spawning player {normalizedId} after GNSS arrived.");
            ProcessPlayerPayload(merged);
            return;
        }

        UpdateSinglePlayer(merged);
    }

    private string NormalizePlayerId(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return "";

        var cleanId = playerId.Split('-')[0];
        return cleanId.Trim().ToLowerInvariant();
    }

    private void AddPlayerToDropdown(string playerId)
    {
        if (playerIds.Contains(playerId))
            return;

        playerIds.Add(playerId);
        playerDropdown.options.Clear();

        foreach (var id in playerIds)
            playerDropdown.options.Add(new TMP_Dropdown.OptionData(id));

        playerDropdown.onValueChanged.RemoveAllListeners();
        playerDropdown.onValueChanged.AddListener(OnPlayerDropdownChanged);

        playerDropdown.value = playerIds.Count - 1;
        playerDropdown.RefreshShownValue();
    }

    private void OnPlayerDropdownChanged(int index)
{
    if (index < 0 || index >= playerIds.Count) return;

    string selectedId = playerIds[index];

    // Hide all panels first
    foreach (var kv in statsPanels)
        kv.Value.SetActive(false);

    // Show selected player's panel
    if (statsPanels.TryGetValue(selectedId, out var panel))
        panel.SetActive(true);
}

    private Vector2 AdjustPositionAvoidingCorners(Vector2 pos)
    {
        // Clamp to rectangular rink first
        pos.x = Mathf.Clamp(pos.x, rinkMin.x, rinkMax.x);
        pos.y = Mathf.Clamp(pos.y, rinkMin.y, rinkMax.y);

        // Adjust for rounded corners
        Vector2[] corners = new Vector2[]
        {
            new Vector2(rinkMin.x, rinkMin.y), // Bottom-left
            new Vector2(rinkMin.x, rinkMax.y), // Top-left
            new Vector2(rinkMax.x, rinkMin.y), // Bottom-right
            new Vector2(rinkMax.x, rinkMax.y)  // Top-right
        };

        foreach (var corner in corners)
        {
            Vector2 toCorner = pos - corner;
            if (toCorner.magnitude < cornerRadius)
            {
                Vector2 dir = toCorner.normalized;
                pos = corner + dir * cornerRadius;
            }
        }

        return pos;
    }

    public Vector3 GetSafeSpawnPosition()
    {
        Vector3 spawnPos;
        float tries = 0;

        do
        {
            float x = Random.Range(rinkMin.x + spawnMargin, rinkMax.x - spawnMargin);
            float y = Random.Range(rinkMin.y + spawnMargin, rinkMax.y - spawnMargin);
            spawnPos = new Vector3(x, 0, y);
            spawnPos = ClampToRinkBounds(spawnPos);
            tries++;
        }
        while (Vector3.Distance(spawnPos, puckController.transform.position) < 1f && tries < 10);

        return spawnPos;
    }

    private Vector3 ClampToRinkBounds(Vector3 pos)
    {
        float x = Mathf.Clamp(pos.x, rinkMin.x, rinkMax.x);
        float z = Mathf.Clamp(pos.z, rinkMin.y, rinkMax.y);

        Vector3 clamped = new Vector3(x, pos.y, z);

        // Clamp corners (XZ plane)
        Vector2[] corners = new Vector2[]
        {
            new Vector2(rinkMin.x, rinkMin.y),
            new Vector2(rinkMin.x, rinkMax.y),
            new Vector2(rinkMax.x, rinkMin.y),
            new Vector2(rinkMax.x, rinkMax.y)
        };

        foreach (var corner in corners)
        {
            Vector2 pos2D = new Vector2(clamped.x, clamped.z);
            Vector2 toCorner = pos2D - corner;
            if (toCorner.magnitude < cornerRadius)
            {
                Vector2 dir = toCorner.normalized;
                Vector2 adjusted = corner + dir * cornerRadius;
                clamped.x = adjusted.x;
                clamped.z = adjusted.y;
            }
        }

        return clamped;
    }

    public void RemovePlayer(string playerId)
    {
        if (!playerControllers.ContainsKey(playerId))
            return;

        var player = playerControllers[playerId];
        playerControllers.Remove(playerId);

        if (statsPanels.TryGetValue(playerId, out var statsPanel))
        {
            Destroy(statsPanel);
            statsPanels.Remove(playerId);
        }

        Destroy(player.gameObject);

        playerIds.Remove(playerId);
        RefreshPlayerDropdown();
    }

    private void RefreshPlayerDropdown()
    {
        playerDropdown.options.Clear();
        foreach (var id in playerIds)
            playerDropdown.options.Add(new TMP_Dropdown.OptionData(id));

        playerDropdown.RefreshShownValue();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws gizmos in editor to visualize rink bounds, corners, safe area, and GPS origin.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector2 center = (rinkMin + rinkMax) / 2;
        Vector2 size = rinkMax - rinkMin;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.magenta;
        Vector2[][] triangles = new Vector2[][]
        {
            new[] { rinkMin, rinkMin + new Vector2(cornerRadius, 0), rinkMin + new Vector2(0, cornerRadius) },
            new[] { new Vector2(rinkMin.x, rinkMax.y), new Vector2(rinkMin.x + cornerRadius, rinkMax.y), new Vector2(rinkMin.x, rinkMax.y - cornerRadius) },
            new[] { new Vector2(rinkMax.x, rinkMin.y), new Vector2(rinkMax.x - cornerRadius, rinkMin.y), new Vector2(rinkMax.x, rinkMin.y + cornerRadius) },
            new[] { rinkMax, rinkMax - new Vector2(cornerRadius, 0), rinkMax - new Vector2(0, cornerRadius) }
        };

        // Draw corner triangles
        foreach (var tri in triangles)
        {
            Gizmos.DrawLine(tri[0], tri[1]);
            Gizmos.DrawLine(tri[1], tri[2]);
            Gizmos.DrawLine(tri[2], tri[0]);
        }

        // Draw GPS origin sphere and label if set
        if (gpsOriginSet)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(gpsOriginMeters, 0.5f);
            Handles.Label(gpsOriginMeters, "GPS Origin");
        }

        // Draw safe spawn area inside rink bounds considering margin
        Gizmos.color = Color.yellow;
        Vector2 safeCenter = (rinkMin + rinkMax) / 2f;
        Vector2 safeSize = rinkMax - rinkMin - new Vector2(spawnMargin * 2, spawnMargin * 2);
        Gizmos.DrawWireCube(safeCenter, safeSize);
    }
#endif
}