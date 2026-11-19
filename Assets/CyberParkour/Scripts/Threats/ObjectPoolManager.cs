using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple prefab/template pooling for frequently spawned objects (projectiles, data packets).
/// Templates are inactive scene objects; pooled copies are re-used instead of Instantiate/Destroy every wave.
/// </summary>
public static class ObjectPoolManager
{
    static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    static readonly Dictionary<GameObject, GameObject> origin = new Dictionary<GameObject, GameObject>();
    static Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        Clear();
    }

    static void OnSceneUnloaded(Scene s) { Clear(); }

    static void Clear()
    {
        pools.Clear();
        origin.Clear();
        root = null;
    }

    static Transform Root()
    {
        if (root == null) root = new GameObject("PooledObjects").transform;
        return root;
    }

    public static GameObject Get(GameObject template)
    {
        if (!pools.TryGetValue(template, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[template] = stack;
        }
        GameObject go = null;
        while (stack.Count > 0 && go == null) go = stack.Pop();
        if (go == null)
        {
            go = Object.Instantiate(template, Root());
            origin[go] = template;
        }
        go.SetActive(true);
        return go;
    }

    public static void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (origin.TryGetValue(go, out var template) && pools.TryGetValue(template, out var stack)) stack.Push(go);
        else Object.Destroy(go);
    }
}
