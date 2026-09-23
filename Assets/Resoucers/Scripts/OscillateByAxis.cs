using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class OscillateByAxis : MonoBehaviour
{
    [Header("Limites relativos (negativo e positivo)")]

    [Range(0f, 20f)]
    public float xMin = 0f;
    [Range(0f, 20f)]
    public float xMax = 0f;

    [Range(0f, 20f)]
    public float yMin = 0f;
    [Range(0f, 20f)]
    public float yMax = 0f;
    [Range(0f, 20f)]
    public float zMin = 0f;
    [Range(0f, 20f)]
    public float zMax = 0f;

    [Header("Velocidades por eixo")]
    public float speedX = 2f;
    public float speedY = 0f;
    public float speedZ = 0f;

    private Rigidbody rb;
    private Vector3 startPos;
    private Vector3 direction; // 1 ou -1 por eixo

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        startPos = transform.position;
        direction = Vector3.one; // começa indo positivo em todos os eixos
    }

    void FixedUpdate()
    {
        Vector3 pos = transform.position;
        Vector3 velocity = rb.linearVelocity;

        // X-axis
        if (speedX != 0)
        {
            if (pos.x > startPos.x + xMax)
                direction.x = -1;
            else if (pos.x < startPos.x - xMin)
                direction.x = 1;
            velocity.x = direction.x * speedX;
        }

        // Y-axis
        if (speedY != 0)
        {
            if (pos.y > startPos.y + yMax)
                direction.y = -1;
            else if (pos.y < startPos.y - yMin)
                direction.y = 1;
            velocity.y = direction.y * speedY;
        }

        // Z-axis
        if (speedZ != 0)
        {
            if (pos.z > startPos.z + zMax)
                direction.z = -1;
            else if (pos.z < startPos.z - zMin)
                direction.z = 1;
            velocity.z = direction.z * speedZ;
        }

        rb.linearVelocity = velocity;
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            startPos = transform.position;


        // desenha linhas para X, Y e Z
        Vector3 minPoint = new Vector3(startPos.x - xMin, startPos.y - yMin, startPos.z - zMin);
        Vector3 maxPoint = new Vector3(startPos.x + xMax, startPos.y + yMax, startPos.z + zMax);

        // Linhas de visualização
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(minPoint.x, startPos.y, startPos.z),
                        new Vector3(maxPoint.x, startPos.y, startPos.z));
                        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(startPos.x, minPoint.y, startPos.z),
                        new Vector3(startPos.x, maxPoint.y, startPos.z));
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(new Vector3(startPos.x, startPos.y, minPoint.z),
                        new Vector3(startPos.x, startPos.y, maxPoint.z));

        // Posições extremas (esferas)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new Vector3(startPos.x - xMin, startPos.y, startPos.z), 0.1f);
        Gizmos.DrawSphere(new Vector3(startPos.x - xMax, startPos.y, startPos.z), 0.1f);
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y - yMin, startPos.z), 0.1f);
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y - yMax, startPos.z), 0.1f);
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y, startPos.z - zMin), 0.1f);
        Gizmos.DrawSphere(new Vector3(startPos.x, startPos.y, startPos.z - zMax), 0.1f);
    }
}
