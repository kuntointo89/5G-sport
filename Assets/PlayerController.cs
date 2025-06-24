using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public Text statsText;
    public GameObject statsPanel;
    public List<Vector2> path;
    public float minSpeed = 1f;
    public float maxSpeed = 5f;
    public float acceleration = 2f;

    private float currentSpeed;
    private float speed;
    private int currentIndex = 0;
    private Vector2 targetPosition;
    private Vector2 lastPosition;

    public HRData heartData;
    public ECGData ecgData;

    void Start()
    {
        currentSpeed = minSpeed;
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name}: Path is null or empty!");
            enabled = false;
            return;
        }

        transform.position = path[0];
        targetPosition = path[currentIndex];
        lastPosition = transform.position;

        if (statsPanel != null)
            statsPanel.SetActive(false); // hide panel at start
        if (statsPanel == null) Debug.LogError($"{gameObject.name}: statsPanel is not assigned!");
        if (statsText == null) Debug.LogError($"{gameObject.name}: statsText is not assigned!");

    }

    void Update()
    {
        if (path == null || path.Count < 2) return;

        float targetSpeed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.InverseLerp(60f, 180f, heartData.average));
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        transform.position = Vector2.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) < 0.1f)
        {
            currentIndex = (currentIndex + 1) % path.Count;
            targetPosition = path[currentIndex];
        }

        speed = Vector2.Distance(transform.position, lastPosition) / Time.deltaTime * 3.6f; // m/s to km/h
        lastPosition = transform.position;

        UpdateVisuals();
    }

    void OnMouseDown()
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
            // Only update stats if showing panel
            string rrString = heartData?.rrData != null ? string.Join(", ", heartData.rrData) : "N/A";
            string ecgString = ecgData?.Samples != null ? $"{ecgData.Samples.Count} samples" : "N/A";

            statsText.text = $"Speed: {speed:F2} km/h\n" +
                             $"HR Avg: {heartData?.average:F1}\n" +
                             $"RR: {rrString}\n" +
                             $"ECG: {ecgString}";

            var ecgGraph = statsPanel.GetComponentInChildren<ECGGraph>();
            if (ecgGraph != null && ecgData?.Samples != null)
            {
                ecgGraph.DrawECG(ecgData.Samples);
            }   
        }
    }

        void UpdateVisuals()
        {
            float t = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);
            GetComponent<SpriteRenderer>().color = Color.Lerp(Color.blue, Color.red, t);
        }
    }