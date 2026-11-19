using UnityEngine;

/// <summary>Pooled moving hazard (data packet, message envelope). Released when its lifetime ends or the player respawns.</summary>
public class Projectile : MonoBehaviour
{
    Vector3 velocity;
    float life;
    LevelManager lm;

    public void Launch(Vector3 vel, float lifetime)
    {
        velocity = vel;
        life = lifetime;
    }

    void OnEnable()
    {
        lm = LevelManager.Instance;
        if (lm != null) lm.Respawned += Despawn;
    }

    void OnDisable()
    {
        if (lm != null) lm.Respawned -= Despawn;
    }

    void Despawn() { ObjectPoolManager.Release(gameObject); }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        life -= Time.deltaTime;
        if (life <= 0f) ObjectPoolManager.Release(gameObject);
    }
}
