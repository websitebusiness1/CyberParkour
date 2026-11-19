using UnityEngine;

/// <summary>Solid door that slides open (disabling its colliders) when a challenge is passed.</summary>
public class GateDoor : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(0f, 6.5f, 0f);
    public float speed = 8f;
    public bool IsOpen { get; private set; }

    Vector3 closedPos;
    Collider[] cols;

    void Awake()
    {
        closedPos = transform.position;
        cols = GetComponentsInChildren<Collider>();
    }

    public void Open()
    {
        IsOpen = true;
        foreach (var c in cols) if (c != null) c.enabled = false;
    }

    void Update()
    {
        Vector3 target = IsOpen ? closedPos + openOffset : closedPos;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}
