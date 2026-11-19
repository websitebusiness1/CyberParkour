using UnityEngine;

/// <summary>Moves an object along Z and wraps it (data packets flowing down a bus cable).</summary>
public class LoopMover : MonoBehaviour
{
    public float speed = 10f;
    public float minZ = 0f;
    public float maxZ = 240f;

    void Update()
    {
        Vector3 p = transform.localPosition;
        p.z += speed * Time.deltaTime;
        if (p.z > maxZ) p.z = minZ;
        else if (p.z < minZ) p.z = maxZ;
        transform.localPosition = p;
    }
}
