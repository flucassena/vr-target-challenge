using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private GameObject explosionEffect;
    [SerializeField] private float sweepRadius = 0.045f;

    private Rigidbody body;
    private Vector3 previousPosition;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.radius = Mathf.Max(capsule.radius, 0.75f);
            capsule.height = Mathf.Max(capsule.height, 1.6f);
            capsule.direction = 2;
        }
    }

    void Start()
    {
        previousPosition = transform.position;
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        Vector3 currentPosition = body != null ? body.position : transform.position;
        Vector3 travel = currentPosition - previousPosition;
        float distance = travel.magnitude;

        // Check every collider crossed during this physics step. A single SphereCast could
        // return the gun or another nearby collider first and silently miss the balloon.
        if (distance > 0.0001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(previousPosition, sweepRadius, travel / distance,
                distance, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                if (TryHitBalloon(hit.collider, hit.point))
                    return;
            }
        }

        // Also cover balloons already overlapping the energy sphere at the end of the step.
        Collider[] overlaps = Physics.OverlapSphere(currentPosition, sweepRadius, ~0,
            QueryTriggerInteraction.Collide);
        foreach (Collider overlap in overlaps)
        {
            if (TryHitBalloon(overlap, currentPosition))
                return;
        }

        previousPosition = currentPosition;
    }

    void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        if (TryHitBalloon(collision.collider, hitPoint))
            return;

        // Original target behaviour remains available outside the arena mode.
        if (collision.gameObject.CompareTag("Target"))
        {
            collision.gameObject.GetComponent<AudioSource>()?.Play();
            var positionCollision = collision.transform;
            Destroy(collision.gameObject, 0.2f);
            Instantiate(explosionEffect, positionCollision.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        TryHitBalloon(other, transform.position);
    }

    private bool TryHitBalloon(Collider hitCollider, Vector3 hitPoint)
    {
        if (hitCollider == null || hitCollider.transform.IsChildOf(transform)) return false;
        HoloBalloonTarget balloon = hitCollider.GetComponentInParent<HoloBalloonTarget>();
        if (balloon == null) return false;

        balloon.Hit(hitPoint);
        Destroy(gameObject);
        return true;
    }
}
