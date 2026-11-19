using System.Collections.Generic;
using UnityEngine;

public enum Sfx { Jump, Land, Slide, Coin, Token, Hit, Checkpoint, Complete, Fail, Warning }

/// <summary>
/// Placeholder audio: generates short synthesized tones at runtime so the game has sound feedback
/// without any imported assets. Replace by assigning real AudioClips into the dictionary in Init().
/// </summary>
public class AudioManager : MonoBehaviour
{
    static AudioManager instance;
    AudioSource source;
    readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    readonly Dictionary<Sfx, float> volumes = new Dictionary<Sfx, float>();

    public static void Play(Sfx sfx)
    {
        if (instance == null)
        {
            var go = new GameObject("AudioManager");
            instance = go.AddComponent<AudioManager>();
            instance.Init();
            DontDestroyOnLoad(go);
        }
        if (instance.clips.TryGetValue(sfx, out var clip))
            instance.source.PlayOneShot(clip, instance.volumes[sfx]);
    }

    void Init()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0f;
        Add(Sfx.Jump,       0.5f, 320f, 640f, 0.12f);
        Add(Sfx.Land,       0.4f, 180f, 90f, 0.08f);
        Add(Sfx.Slide,      0.35f, 500f, 200f, 0.25f);
        Add(Sfx.Coin,       0.5f, 880f, 880f, 0.05f, 1320f, 1320f, 0.09f);
        Add(Sfx.Token,      0.5f, 660f, 660f, 0.07f, 880f, 880f, 0.07f, 1320f, 1320f, 0.12f);
        Add(Sfx.Hit,        0.7f, 200f, 60f, 0.3f);
        Add(Sfx.Checkpoint, 0.5f, 523f, 523f, 0.1f, 659f, 659f, 0.1f, 784f, 784f, 0.18f);
        Add(Sfx.Complete,   0.6f, 523f, 523f, 0.12f, 659f, 659f, 0.12f, 784f, 784f, 0.12f, 1046f, 1046f, 0.35f);
        Add(Sfx.Warning,    0.45f, 880f, 880f, 0.09f, 660f, 660f, 0.09f);
        Add(Sfx.Fail,       0.7f, 400f, 300f, 0.25f, 300f, 200f, 0.25f, 200f, 100f, 0.4f);
    }

    // spec = repeating triplets: startFreq, endFreq, duration
    void Add(Sfx sfx, float volume, params float[] spec)
    {
        const int sr = 44100;
        var data = new List<float>();
        for (int i = 0; i + 2 < spec.Length; i += 3)
        {
            float f0 = spec[i], f1 = spec[i + 1], d = spec[i + 2];
            int n = (int)(sr * d);
            float phase = 0f;
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / sr;
                float env = Mathf.Clamp01(t * 20f) * (1f - t);
                data.Add(Mathf.Sin(phase) * env * 0.35f);
            }
        }
        var clip = AudioClip.Create(sfx.ToString(), data.Count, 1, sr, false);
        clip.SetData(data.ToArray(), 0);
        clips[sfx] = clip;
        volumes[sfx] = volume;
    }
}
