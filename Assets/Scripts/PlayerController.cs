using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    // UI & Stats
    [Header("Player Properties")]
    public TMP_Text statsText;
    public GameObject statsPanel;
    public string player_name;

    // Smooth movement settings
    private Vector2 targetPosition;
    private Vector2 velocity; 
    public float smoothTime = 1.0f; // smaller = snappier, larger = smoother

    [Header("Movement Speeds (km/h)")]
    [SerializeField] private float minSpeed = 5f;   // User sets this in inspector (km/h or m/s)
    [SerializeField] private float maxSpeed = 25f;  // User sets this in inspector (km/h or m/s)
    [SerializeField] private bool valuesAreInKmh = true;
    public float acceleration = 15f;

    // Rink boundaries & movement config
    public Vector2 rinkMin, rinkMax;
    [Range(0f, 5f)] public float cornerRadius = 2.5f;
    // Internal state
    private float currentSpeed;
    private int currentHeartRate = 60;
    private Vector2 currentPosition, lastReceivedPosition = Vector2.negativeInfinity;

    // ECG
    private Queue<int> realTimeECGBuffer = new Queue<int>(500);
    private ECGGraph ecgGraph;
    private float statsUpdateInterval = 0.5f;
    private float statsUpdateTimer = 0f;

    // UI Elements
    private TMP_Text numberText;
    private Transform arrowPivot, arrowTransform;
    public float arrowOrbitRadius = 1f;

    // Movement helpers
    private Vector2 lastValidDirection = Vector2.right;
    private const float movementThreshold = 0.0001f;
    private Vector2 lastPosition;
    private float freezeTimer = 0f;

    // Physics
    private Rigidbody2D rb;

    void Awake()
    {
        targetPosition = transform.position;
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogError("PlayerController requires a Rigidbody2D component.");
    }

    // Setup initial state for real-time control
    public void InitializeRealtime()
    {
        targetPosition = transform.position; // sync at start
        currentPosition = rb != null ? rb.position : (Vector2)transform.position;

        SetupPlayerNumberUI();
        SetupArrow();
        ClampAndMoveToTarget();
        SetupECGGraph();
    }

    private void SetupPlayerNumberUI()
    {
        Transform numberCanvas = transform.Find("NumberCanvas");
        if (numberCanvas == null) return;

        numberText = numberCanvas.GetComponentInChildren<TMP_Text>();
        if (numberText != null && !string.IsNullOrEmpty(player_name))
        {
            string[] parts = player_name.Split('_');
            numberText.text = (parts.Length > 1 && int.TryParse(parts[1], out int num)) ? num.ToString() : player_name;
        }
    }

    private void SetupArrow()
    {
        arrowPivot = transform.Find("ArrowPivot");
        if (arrowPivot == null) return;

        arrowPivot.localPosition = Vector3.zero;

        Vector2 initialDir = targetPosition - currentPosition;
        float angle = (initialDir.sqrMagnitude > movementThreshold) 
            ? Mathf.Atan2(initialDir.y, initialDir.x) * Mathf.Rad2Deg 
            : 0f;

        arrowPivot.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        arrowTransform = arrowPivot.Find("Arrow");
        if (arrowTransform != null)
        {
            arrowTransform.localPosition = Vector3.up * arrowOrbitRadius;
            arrowTransform.localRotation = Quaternion.identity;
        }
    }

    private void ClampAndMoveToTarget()
    {
        targetPosition = new Vector2(
            Mathf.Clamp(targetPosition.x, rinkMin.x, rinkMax.x),
            Mathf.Clamp(targetPosition.y, rinkMin.y, rinkMax.y)
        );

        rb.MovePosition(targetPosition);
        lastPosition = targetPosition;
    }

    private void SetupECGGraph()
    {
        if (statsPanel == null) return;

        ecgGraph = statsPanel.GetComponentInChildren<ECGGraph>();
        if (ecgGraph != null)
            StartCoroutine(FeedRealTimeECG());
    }

    // Called by external systems to update player movement
    public void UpdatePosition(Vector2 pos)
    {
        Vector2 clamped = ClampToTriangularCorners(pos);
        if (clamped != lastReceivedPosition)
            lastReceivedPosition = clamped;

        targetPosition = clamped;
        Debug.DrawLine(clamped, clamped + Vector2.up * 0.5f, Color.magenta, 1f);
    }

    public void UpdateIMU(IMUData IMU)
    {
        // Example: Use the last acceleration reading
        if (IMU.ArrayAcc?.Count > 0)
        {
            var last = IMU.ArrayAcc[^1]; // C# 8+ syntax for last element
            Vector3 accVec = new Vector3(last.x, last.y, last.z);
            // Use for tilt, rotation, or speed influence
        }
    }

    public void UpdateHeartRate(int hr)
    {
        currentHeartRate = hr;
    }

    public void ReceiveECGSample(int sample)
    {
        if (realTimeECGBuffer.Count > 500)
            realTimeECGBuffer.Dequeue();

        realTimeECGBuffer.Enqueue(sample);
    }

    void Update()
    {
        UpdateSpeedBasedOnHR();
        UpdateArrowRotation();
        UpdateVisuals();

        // Update stats text at intervals
        statsUpdateTimer += Time.deltaTime;
        if (statsPanel != null && statsPanel.activeSelf && statsText != null && statsUpdateTimer >= statsUpdateInterval)
        {
            statsUpdateTimer = 0f;
            statsText.text = $"HR: {currentHeartRate:F1} bpm\nECG:";
        }
    }

    public void SetTargetPosition(Vector2 newPos)
    {
        targetPosition = newPos;
    }

    private void UpdateSpeedBasedOnHR()
    {

        // Map HR 60 → 5 km/h, HR 180 → 25 km/h
        // Normalize HR (60 → 0, 180 → 1)
        float normalizedHR = Mathf.Clamp01(Mathf.InverseLerp(80f, 180f, currentHeartRate));

        // Apply curve for realism
        float curvedHR = Mathf.Sqrt(normalizedHR);

        // Convert input values to m/s if needed
        float minMs = valuesAreInKmh ? minSpeed / 3.6f : minSpeed;
        float maxMs = valuesAreInKmh ? maxSpeed / 3.6f : maxSpeed;

        // Lerp between speeds in m/s
        float targetSpeedMs = Mathf.Lerp(minMs, maxMs, curvedHR);

        // Smooth acceleration
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeedMs, acceleration * Time.deltaTime);

    }

    private void UpdateArrowRotation()
    {
        if (arrowPivot == null) return;

        Vector2 dir = targetPosition - currentPosition;
        float distance = dir.magnitude;

        if (distance > 0.05f)
        {
            lastValidDirection = dir.normalized;
            float angle = Mathf.Atan2(lastValidDirection.y, lastValidDirection.x) * Mathf.Rad2Deg;
            arrowPivot.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            arrowPivot.gameObject.SetActive(true);
        }
        else
        {

            arrowPivot.gameObject.SetActive(true);
        }
    }

   private void FixedUpdate()
{
    if (rb == null) return;

    // Direction to move: toward target if available, else keep last direction
    Vector2 dir = targetPosition - rb.position;
    if (dir.magnitude < 0.01f)
    {
        dir = lastValidDirection; // fallback to previous direction
    }
    else
    {
        lastValidDirection = dir.normalized; // update last valid direction
    }

    dir.Normalize();

    // Calculate movement step based on currentSpeed
    Vector2 step = dir * currentSpeed * Time.fixedDeltaTime;

    // Move player
    Vector2 newPos = rb.position + step;

    // Clamp position inside rink boundaries
    newPos = new Vector2(
        Mathf.Clamp(newPos.x, rinkMin.x, rinkMax.x),
        Mathf.Clamp(newPos.y, rinkMin.y, rinkMax.y)
    );

    rb.MovePosition(newPos);
    currentPosition = newPos;
}

            void LateUpdate()
            {
                if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
                    freezeTimer += Time.deltaTime;
                else
                    freezeTimer = 0f;

                if (freezeTimer > 3f)
                    Debug.LogWarning($"Player {player_name} appears frozen!");

                lastPosition = transform.position;
            }

    private void UpdateVisuals()
    {
        float t = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = Color.Lerp(Color.blue, Color.red, t); // Color reflects speed
    }

    public void UpdateStatsText()
    {
        if (statsText != null)
        {
            statsText.text = $"HR: {currentHeartRate:F1} bpm\nSpeed: {currentSpeed * 3.6f:F1} km/h\nECG:";
        }
    }

    // Triangle check for soft corner clamping
    private bool PointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        float sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        bool b1 = sign(pt, v1, v2) < 0f;
        bool b2 = sign(pt, v2, v3) < 0f;
        bool b3 = sign(pt, v3, v1) < 0f;

        return b1 == b2 && b2 == b3;
    }

    // Prevents players from entering rounded triangle corners
    private Vector2 ClampToTriangularCorners(Vector2 pos)
    {
        Vector2 clamped = new Vector2(
            Mathf.Clamp(pos.x, rinkMin.x, rinkMax.x),
            Mathf.Clamp(pos.y, rinkMin.y, rinkMax.y)
        );

        Vector2[][] triangles =
        {
            new Vector2[] { rinkMin, rinkMin + new Vector2(cornerRadius, 0), rinkMin + new Vector2(0, cornerRadius) },
            new Vector2[] { new Vector2(rinkMin.x, rinkMax.y), new Vector2(rinkMin.x + cornerRadius, rinkMax.y), new Vector2(rinkMin.x, rinkMax.y - cornerRadius) },
            new Vector2[] { new Vector2(rinkMax.x, rinkMin.y), new Vector2(rinkMax.x - cornerRadius, rinkMin.y), new Vector2(rinkMax.x, rinkMin.y + cornerRadius) },
            new Vector2[] { rinkMax, rinkMax - new Vector2(cornerRadius, 0), rinkMax - new Vector2(0, cornerRadius) }
        };

        Vector2 center = (rinkMin + rinkMax) / 2f;
        int step = 0, maxSteps = 20;

        for (int i = 0; i < triangles.Length; i++)
        {
            while (PointInTriangle(clamped, triangles[i][0], triangles[i][1], triangles[i][2]) && step < maxSteps)
            {
                clamped = Vector2.MoveTowards(clamped, center, 0.1f);
                step++;
            }
        }

        return clamped;
    }

    // Continuously feeds ECG graph at 50Hz
    private System.Collections.IEnumerator FeedRealTimeECG()
    {
        while (true)
        {
            if (statsPanel?.activeSelf == true && ecgGraph != null && realTimeECGBuffer.Count > 0)
            {
                int sample = realTimeECGBuffer.Dequeue();
                ecgGraph.AddSample(sample);
            }
            yield return new WaitForSeconds(0.02f); // 50Hz
        }
    }

    public void UpdateECGFrame(List<int> samples)
    {
        if (samples == null || samples.Count == 0) return;

    // Clear previous buffer to reflect the frame
        realTimeECGBuffer.Clear();

        foreach (var s in samples)
            realTimeECGBuffer.Enqueue(s);
    }

    // Draw rink bounds and corner radius spheres in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 center = (rinkMin + rinkMax) / 2f;
        Vector3 size = new Vector3(rinkMax.x - rinkMin.x, rinkMax.y - rinkMin.y, 0f);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.cyan;
        Vector3[] corners =
        {
            new Vector3(rinkMin.x + cornerRadius, rinkMin.y + cornerRadius),
            new Vector3(rinkMin.x + cornerRadius, rinkMax.y - cornerRadius),
            new Vector3(rinkMax.x - cornerRadius, rinkMin.y + cornerRadius),
            new Vector3(rinkMax.x - cornerRadius, rinkMax.y - cornerRadius)
        };

        foreach (var c in corners)
            Gizmos.DrawWireSphere(c, cornerRadius);
    }
}