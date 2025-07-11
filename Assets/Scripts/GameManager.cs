using UnityEngine;
using NativeWebSocket;
using System.Collections.Generic;
using System.Text;
using TMPro;
using SimpleJSON;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    [Header("Rink Corners")]
    public float cornerRadius = 2.5f;  // Radius to define rounded corners of rink bounds

    [Header("Safe Spawn Area")]
    public float spawnMargin = 3f; // Margin inside rink edges to restrict player/puck spawn area

    [Header("Player List UI")]
    public TMP_Dropdown playerDropdown;  // Dropdown UI to select players
    public Transform playerListParent;   // Parent transform for player UI elements (not used in code, might be for future use)

    [Header("Prefabs & UI")]
    public GameObject playerPrefab;      // Player GameObject prefab to instantiate
    public GameObject statsPanelPrefab;  // Stats panel prefab to show player info on screen

    [Header("Rink Bounds (in meters)")]
    public Vector2 rinkMin = new Vector2(-30, -15); // Bottom-left corner of rink area in meters
    public Vector2 rinkMax = new Vector2(30, 15);   // Top-right corner of rink area in meters

    [Header("Puck Reference")]
    public PuckMovement puckController;  // Reference to puck controller script

    // List of player IDs for dropdown management
    private List<string> playerIds = new();

    // Buffer for initial player data received before GPS origin is set
    private List<RealtimePlayerUpdate> initialPlayersBuffer = new();

    private const int initialPlayersCount = 5; // Number of players required to set GPS origin

    // Dictionary mapping player IDs to their PlayerController instances
    private Dictionary<string, PlayerController> playerControllers = new();

    // Dictionary mapping player IDs to their corresponding stats panel GameObjects
    private Dictionary<string, GameObject> statsPanels = new();

    private WebSocket websocket;  // WebSocket connection to server
    private Canvas screenCanvas;  // Screen space canvas to parent UI elements

    private double baseLat = 0;   // Latitude used as GPS origin reference
    private double baseLon = 0;   // Longitude used as GPS origin reference
    private Vector2 gpsOriginMeters = Vector2.zero;  // Origin position in meters (not updated in code)
    private bool gpsOriginSet = true;  // Flag indicating if GPS origin has been set

    void Awake()
    {
        // Find the screen space canvas in the scene for UI placement
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                screenCanvas = canvas;
                break;
            }
        }

        if (!screenCanvas)
            Debug.LogError("[Init] No screen-space canvas found!");
    }

    async void Start()
    {
        // Initialize WebSocket connection to server
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () => Debug.Log("WebSocket connected!");
        websocket.OnError += e => Debug.LogError("WebSocket error: " + e);
        websocket.OnClose += e => Debug.Log("WebSocket closed.");

        // Define handler for incoming messages from WebSocket
        websocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            HandleIncomingMessage(message);
        };

        await websocket.Connect(); // Connect asynchronously
    }

    void Update()
    {
        // Dispatch any queued WebSocket messages to handlers
        websocket?.DispatchMessageQueue();

        // Detect if user clicks outside any player to hide stats panels
        HandleClickOutsidePlayer();
    }

    private void HandleClickOutsidePlayer()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Convert mouse position to world coordinates
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Check if any 2D collider exists under mouse
            Collider2D hit = Physics2D.OverlapPoint(mousePos);

            // If no collider or collider is not a player, hide all player stats panels
            if (!hit || hit.GetComponent<PlayerController>() == null)
            {
                foreach (var pc in playerControllers.Values)
                    pc.statsPanel?.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Parses incoming JSON message from server and updates players and puck data accordingly.
    /// </summary>
    /// <param name="message">Raw JSON message string</param>
    private void HandleIncomingMessage(string message)
    {
        try
        {
            var root = JSON.Parse(message);

            // Process each player update in the "players" array
            foreach (JSONNode node in root["players"].AsArray)
            {
                var update = JsonUtility.FromJson<RealtimePlayerUpdate>(node.ToString());

                if (!string.IsNullOrEmpty(update?.playerId))
                {
                    Debug.Log($"Parsed update: PlayerID={update.playerId}, ECG={update.ecgSample}, HR={update.hrValue}");
                    ProcessRealtimeData(update);
                }
                else
                {
                    Debug.LogWarning($"Invalid player data:\n{node}");
                }
            }

            // If puck data is present, update puck position and speed
            if (root.HasKey("puck"))
            {
                var puckNode = root["puck"];
                UpdatePuckFromServer(
                    puckNode["latitude"].AsFloat,
                    puckNode["longitude"].AsFloat,
                    puckNode["speed"].AsFloat
                );
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to parse message: " + ex.Message);
        }
    }

    /// <summary>
    /// Updates puck position based on GPS data from server, clamped within rink safe zone.
    /// </summary>
    /// <param name="lat">Latitude of puck</param>
    /// <param name="lon">Longitude of puck</param>
    /// <param name="speed">Speed of puck</param>
    private void UpdatePuckFromServer(float lat, float lon, float speed)
    {
        if (!gpsOriginSet || puckController == null) return;

        // Convert GPS coords to meters relative to GPS origin
        Vector2 posMeters = GPSUtils.GPSToMeters(lat, lon, baseLat, baseLon);

        // Define safe spawn bounds with margins inside rink edges
        float minX = rinkMin.x + spawnMargin;
        float maxX = rinkMax.x - spawnMargin;
        float minY = rinkMin.y + spawnMargin;
        float maxY = rinkMax.y - spawnMargin;

        // Clamp puck position inside safe bounds
        Vector2 clamped = new(
            Mathf.Clamp(posMeters.x, minX, maxX),
            Mathf.Clamp(posMeters.y, minY, maxY)
        );

        // Update puck controller with clamped position and speed
        puckController.UpdatePuck(clamped, speed);
    }

    /// <summary>
    /// Processes incoming player data: buffers initial players to compute GPS origin,
    /// then updates or creates player GameObjects accordingly.
    /// </summary>
    /// <param name="update">Realtime player data update</param>
    public void ProcessRealtimeData(RealtimePlayerUpdate update)
    {
        // If GPS origin not set, buffer initial player data to calculate origin
        if (!gpsOriginSet)
        {
            initialPlayersBuffer.Add(update);

            if (initialPlayersBuffer.Count >= initialPlayersCount)
            {
                // Calculate average latitude and longitude as GPS origin
                baseLat = baseLon = 0;
                foreach (var p in initialPlayersBuffer)
                {
                    baseLat += p.latitude;
                    baseLon += p.longitude;
                }

                baseLat /= initialPlayersBuffer.Count;
                baseLon /= initialPlayersBuffer.Count;
                gpsOriginSet = true;

                // Process buffered players now that origin is set
                foreach (var p in initialPlayersBuffer)
                    ProcessPlayer(p);

                initialPlayersBuffer.Clear();
            }
            return;
        }

        // GPS origin is set, process the player update immediately
        ProcessPlayer(update);
    }

    /// <summary>
    /// Updates existing player or creates new player GameObject based on realtime update.
    /// Clamps position within rink bounds and adjusts to avoid corners.
    /// </summary>
    /// <param name="update">Realtime player update data</param>
    private void ProcessPlayer(RealtimePlayerUpdate update)
    {
        // Convert GPS to meters relative to origin
        Vector2 meters = GPSUtils.GPSToMeters(update.latitude, update.longitude, baseLat, baseLon);

        // Calculate safe area boundaries within rink
        float minX = rinkMin.x + spawnMargin;
        float maxX = rinkMax.x - spawnMargin;
        float minY = rinkMin.y + spawnMargin;
        float maxY = rinkMax.y - spawnMargin;

        // Clamp position inside safe bounds
        Vector2 clamped = new(
            Mathf.Clamp(meters.x, minX, maxX),
            Mathf.Clamp(meters.y, minY, maxY)
        );

        // Adjust position to avoid corner triangles (rounded corners)
        clamped = AdjustPositionAvoidingCorners(clamped);

        // Get latest ECG sample if available
        int? latestEcgSample = (update.ecgSample?.Length ?? 0) > 0
            ? update.ecgSample[^1]
            : null;

        // If player already exists, update position and stats
        if (playerControllers.TryGetValue(update.playerId, out var pc))
        {
            pc.UpdatePosition(clamped);

            if (latestEcgSample.HasValue)
                pc.ReceiveECGSample(latestEcgSample.Value);

            pc.UpdateHeartRate(update.hrValue);
        }
        else
        {
            // If player or stats prefabs or canvas missing, exit
            if (!playerPrefab || !statsPanelPrefab || !screenCanvas) return;

            // Instantiate player and stats panel UI
            var player = Instantiate(playerPrefab, clamped, Quaternion.identity);
            var statsPanel = Instantiate(statsPanelPrefab, screenCanvas.transform);
            var newPc = player.GetComponent<PlayerController>();

            if (!newPc) return;

            // Initialize PlayerController properties
            newPc.playerId = update.playerId;
            newPc.statsPanel = statsPanel;
            newPc.statsText = statsPanel.GetComponentInChildren<TMP_Text>();
            newPc.rinkMin = rinkMin;
            newPc.rinkMax = rinkMax;

            // Update position and initialize realtime data
            newPc.UpdatePosition(clamped);
            newPc.InitializeRealtime();

            if (latestEcgSample.HasValue)
                newPc.ReceiveECGSample(latestEcgSample.Value);

            newPc.UpdateHeartRate(update.hrValue);

            // Show stats panel by default
            newPc.statsPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();

            // Store references for management
            playerControllers[update.playerId] = newPc;
            statsPanels[update.playerId] = statsPanel;

            // Add to dropdown and show stats
            AddPlayerToDropdown(update.playerId);
            ShowPlayerStats(update.playerId);

            Debug.Log($"New player {update.playerId} | ECG={update.ecgSample} | HR={update.hrValue}");
        }
    }

    /// <summary>
    /// Adds a new player ID to the dropdown UI and refreshes it.
    /// </summary>
    /// <param name="playerId">Player ID to add</param>
    private void AddPlayerToDropdown(string playerId)
    {
        playerIds.Add(playerId);
        playerDropdown.options.Clear();

        foreach (var id in playerIds)
            playerDropdown.options.Add(new TMP_Dropdown.OptionData(id));

        // Remove old listeners and add new handler for dropdown change
        playerDropdown.onValueChanged.RemoveAllListeners();
        playerDropdown.onValueChanged.AddListener(OnPlayerDropdownChanged);

        // Set dropdown to the last added player and refresh UI
        playerDropdown.value = playerIds.Count - 1;
        playerDropdown.RefreshShownValue();

        // Invoke callback for selected player
        playerDropdown.onValueChanged.Invoke(playerDropdown.value);
    }

    /// <summary>
    /// Called when dropdown player selection changes.
    /// </summary>
    /// <param name="index">Selected dropdown index</param>
    public void OnPlayerDropdownChanged(int index)
    {
        if (index >= 0 && index < playerIds.Count)
            ShowPlayerStats(playerIds[index]);
    }

    /// <summary>
    /// Shows stats panel for the selected player and hides others.
    /// </summary>
    /// <param name="playerId">Player ID to show stats for</param>
    public void ShowPlayerStats(string playerId)
    {
        // Hide all other players' stats panels
        foreach (var p in playerControllers.Values)
            p.statsPanel?.SetActive(false);

        // Show selected player's stats panel and update text
        if (playerControllers.TryGetValue(playerId, out var pc) && pc.statsPanel != null)
        {
            pc.statsPanel.SetActive(true);
            pc.UpdateStatsText();
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <summary>
    /// Adjusts a given position vector to avoid overlapping rink corners.
    /// Uses a set of triangular regions at corners.
    /// </summary>
    /// <param name="pos">Position to adjust</param>
    /// <returns>Adjusted position avoiding corners</returns>
    private Vector2 AdjustPositionAvoidingCorners(Vector2 pos)
    {
        // Define four corner triangles based on rink corners and corner radius
        Vector2[][] triangles = new Vector2[][]
        {
            new[] { rinkMin, rinkMin + new Vector2(cornerRadius, 0), rinkMin + new Vector2(0, cornerRadius) },
            new[] { new Vector2(rinkMin.x, rinkMax.y), new Vector2(rinkMin.x + cornerRadius, rinkMax.y), new Vector2(rinkMin.x, rinkMax.y - cornerRadius) },
            new[] { new Vector2(rinkMax.x, rinkMin.y), new Vector2(rinkMax.x - cornerRadius, rinkMin.y), new Vector2(rinkMax.x, rinkMin.y + cornerRadius) },
            new[] { rinkMax, rinkMax - new Vector2(cornerRadius, 0), rinkMax - new Vector2(0, cornerRadius) }
        };

        // Center point of rink, used for direction to push position out of corners
        Vector2 center = (rinkMin + rinkMax) / 2f;

        foreach (var tri in triangles)
        {
            // If position is inside any corner triangle, push it out along vector from position to center
            if (PointInTriangle(pos, tri[0], tri[1], tri[2]))
                return tri[0] + (center - pos).normalized * (cornerRadius * 1.2f);
        }

        return pos; // No adjustment needed
    }

    /// <summary>
    /// Helper method to check if point p is inside triangle formed by points a, b, c.
    /// </summary>
    private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float Sign(Vector2 v1, Vector2 v2, Vector2 v3) =>
            (v1.x - v3.x) * (v2.y - v3.y) - (v2.x - v3.x) * (v1.y - v3.y);

        bool b1 = Sign(p, a, b) < 0.0f;
        bool b2 = Sign(p, b, c) < 0.0f;
        bool b3 = Sign(p, c, a) < 0.0f;

        return b1 == b2 && b2 == b3;
    }

    /// <summary>
    /// Ensures WebSocket is properly closed when application quits.
    /// </summary>
    private async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
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
