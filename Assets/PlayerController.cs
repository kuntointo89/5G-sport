using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


// This scripts main purpose is to control the movement of the players

public class PlayerController : MonoBehaviour
{
    // UI elements for displaying stats
    public Text statsText;
    public GameObject statsPanel;

    // List of positions representing the player path
    public List<Vector2> path;

    // Movement speed parameters
    public float minSpeed = 1f;
    public float maxSpeed = 5f;
    public float acceleration = 2f;

    // Internal state variables
    private float currentSpeed;
    private float speed;
    private int currentIndex = 0;
    private Vector2 targetPosition;
    private Vector2 lastPosition;

    // Input data references
    public HRData heartData;
    public ECGData ecgData;

    void Start()
    {
        // Initialize speed
        currentSpeed = minSpeed;

        // Validate path data
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: Path is null or empty!");
            enabled = false;
            return;
        }

        // Set initial position
        transform.position = path[0];
        targetPosition = path[currentIndex];
        lastPosition = transform.position;

        // Hide stats panel initially
        if (statsPanel != null)
            statsPanel.SetActive(false);
        if (statsPanel == null) Debug.LogError($"{gameObject.name}: statsPanel is not assigned!");
        if (statsText == null) Debug.LogError($"{gameObject.name}: statsText is not assigned!");
    }

    void Update()
    {
        // Skip update if path is invalid
        if (path == null || path.Count < 2) return;

        // Determine target speed based on heart rate
        float targetSpeed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.InverseLerp(60f, 180f, heartData.average));
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        // Move toward next target point
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);

        // If close to target, proceed to next point
        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
        {
            currentIndex = (currentIndex + 1) % path.Count;
            targetPosition = path[currentIndex];
        }

        // Calculate current speed in km/h. The speed is changed from m/s to km/h.
        speed = Vector2.Distance(transform.position, lastPosition) / Time.deltaTime * 3.6f;
        lastPosition = transform.position;

        // Update player visuals
        UpdateVisuals();
    }

    void OnMouseDown()

        //When a player is clicked, a panel will be displayed. When clicked again, the panel will disappear.
    
    {
        if (statsPanel == null)
        {
            Debug.LogWarning("statsPanel is not assigned!");
            return;
        }

        // Toggle panel visibility
        bool isActive = statsPanel.activeSelf;
        statsPanel.SetActive(!isActive);

        if (!isActive)
        {
            // Populate stats when panel is shown
            if (ecgData?.Samples != null)
            {
                Debug.Log($"{gameObject.name} ECG sample count: {ecgData.Samples.Count}");
                Debug.Log($"First few samples: {string.Join(", ", ecgData.Samples.GetRange(0, Mathf.Min(10, ecgData.Samples.Count)))}");
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} has no ECG sample data.");
            }
            // Only update stats if showing panel
            string rrString = heartData?.rrData != null ? string.Join(", ", heartData.rrData) : "N/A";
            string ecgString = ecgData?.Samples != null ? $"{ecgData.Samples.Count} samples" : "N/A";

            statsText.text = $"Speed: {speed:F2} km/h\n" +
                             $"HR Avg: {heartData?.average:F1}\n" +
                             $"RR: {rrString}\n" +
                             $"ECG: {ecgString}";

            // Attempt to draw ECG Graph

            var ecgGraph = statsPanel.GetComponentInChildren<ECGGraph>();
            if (ecgGraph != null && ecgData?.Samples != null)
            {
                ecgGraph.DrawECG(ecgData.Samples);
            }
        }
    }


    void UpdateVisuals()
    {
        // Visual indication of speed via color
        float t = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);
        GetComponent<SpriteRenderer>().color = Color.Lerp(Color.blue, Color.red, t);
    }
}
