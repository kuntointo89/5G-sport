using UnityEngine;

public class PuckMovement : MonoBehaviour
{
    public float speed = 10f;
    public Vector2 direction = new Vector2(1, 0).normalized; // initial movement direction

    private Rigidbody2D rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = direction * speed; // set puck's velocity at start
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            // Reflect puck direction when hitting a player

            Vector2 playerVelocity = collision.relativeVelocity;
            direction = Vector2.Reflect(direction, collision.contacts[0].normal);
            rb.linearVelocity = direction * speed;
        }
    }
}

