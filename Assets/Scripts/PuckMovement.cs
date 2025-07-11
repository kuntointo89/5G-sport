using UnityEngine;

public class PuckMovement : MonoBehaviour
{
    private Vector3 targetPosition;
    public float lerpSpeed = 10f; // Adjust for responsiveness

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
    }

    public void UpdatePuck(Vector2 newPosition, float speed)
    {
        targetPosition = new Vector3(newPosition.x, transform.position.y, newPosition.y);
    }
}