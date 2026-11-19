using System.Collections;
using UnityEngine;

/// <summary>Trigger collectible. Collected flag prevents double-collection. Restore() is used when
/// coins are forfeited after a respawn.</summary>
public class CoinCollectible : MonoBehaviour
{
    public bool isSecurityToken;
    public float spinSpeed = 120f;
    public float bobHeight = 0.12f;

    public bool Collected { get; private set; }

    Collider col;
    Vector3 startPos;
    Vector3 baseScale;
    float bobOffset;

    void Awake()
    {
        col = GetComponentInChildren<Collider>();
        startPos = transform.position;
        baseScale = transform.localScale;
        bobOffset = Random.value * 6.28f;
    }

    void Update()
    {
        if (Collected) return;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * 3f + bobOffset) * bobHeight);
    }

    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        if (col != null) col.enabled = false;
        if (LevelManager.Instance != null) LevelManager.Instance.OnCoinCollected(this);
        StartCoroutine(CollectAnimation());
    }

    IEnumerator CollectAnimation()
    {
        const float duration = 0.25f;
        float t = 0f;
        Vector3 from = transform.position;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = t / duration;
            transform.position = from + Vector3.up * (k * 1.2f);
            transform.localScale = baseScale * Mathf.Lerp(1f, 1.8f, k) * (1f - k * k);
            transform.Rotate(0f, 720f * Time.unscaledDeltaTime, 0f, Space.World);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    public void Restore()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        Collected = false;
        transform.localScale = baseScale;
        transform.position = startPos;
        if (col != null) col.enabled = true;
    }
}
