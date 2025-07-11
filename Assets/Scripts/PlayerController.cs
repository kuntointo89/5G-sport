using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerController : MonoBehaviour
{
    // UI & Stats
    public TMP_Text statsText;
    public GameObject statsPanel;
    public string playerId;

    // Rink boundaries & movement config
    public Vector2 rinkMin, rinkMax;
    [Range(0f, 5f)] public float cornerRadius = 2.5f;
    public float minSpeed = 1f, maxSpeed = 5f, acceleration = 2f;

    // Internal state
    private float currentSpeed;
    private float heartRate = 60f;
    private Vector2 currentPosition, targetPosition, lastReceivedPosition = Vector2.negativeInfinity;

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
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogError("PlayerController requires a Rigidbody2D component.");
    }

    // Setup initial state for real-time control
    public void InitializeRealtime()
    {
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
        if (numberText != null && !string.IsNullOrEmpty(playerId))
        {
            string[] parts = playerId.Split('_');
            numberText.text = (parts.Length > 1 && int.TryParse(parts[1], out int num)) ? num.ToString() : playerId;
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

    public void UpdateHeartRate(float hr) => heartRate = hr;

    public void ReceiveECGSample(int sample)
    {
        Debug.Log($"Received ECG sample: {sample}");
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
            statsText.text = $"HR: {heartRate:F1} bpm\nSpeed: {currentSpeed * 3.6f:F1} km/h\nECG:";
        }
    }

    private void UpdateSpeedBasedOnHR()
    {
        float targetSpeed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.InverseLerp(60f, 180f, heartRate));
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
    }

    private void UpdateArrowRotation()
    {
        if (arrowPivot == null) return;

        Vector2 dir = targetPosition - currentPosition;
        float distance = dir.magnitude;

        if (distance > movementThreshold)
        {
            lastValidDirection = dir.normalized;
        }

        if (lastValidDirection.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(lastValidDirection.y, lastValidDirection.x) * Mathf.Rad2Deg;
            arrowPivot.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        arrowPivot.gameObject.SetActive(true);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        currentPosition = rb.position;
        float distanceToTarget = Vector2.Distance(currentPosition, targetPosition);
        Vector2 delta = currentPosition - lastPosition;
        Debug.Log($"[Movement Δ] {playerId}: {delta.magnitude:F5}");

        if (distanceToTarget > 0.01f)
        {
            Vector2 newPos = Vector2.MoveTowards(currentPosition, targetPosition, currentSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
            lastPosition = newPos;
        }
        else
        {
            rb.MovePosition(targetPosition);
            lastPosition = targetPosition;
        }
    }

    void LateUpdate()
    {
        if (Vector2.Distance(lastPosition, transform.position) < 0.01f)
            freezeTimer += Time.deltaTime;
        else
            freezeTimer = 0f;

        if (freezeTimer > 3f)
            Debug.LogWarning($"Player {playerId} appears frozen!");

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
            statsText.text = $"HR: {heartRate:F1} bpm\nSpeed: {currentSpeed * 3.6f:F1} km/h\nECG:";
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
