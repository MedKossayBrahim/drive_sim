using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class BoatSoundEffects : MonoBehaviour
{
    AudioSource source;
    AudioClip coin;
    AudioClip powerUp;
    AudioClip hit;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.55f;
        coin = BuildTone("Coin Pickup", 0.16f, 720f, 1240f, 0.18f);
        powerUp = BuildTone("Power-Up Pickup", 0.34f, 390f, 1180f, 0.24f);
        hit = BuildHit();
    }

    public void PlayCoin() { if (source != null && coin != null) source.PlayOneShot(coin); }
    public void PlayPowerUp() { if (source != null && powerUp != null) source.PlayOneShot(powerUp); }
    public void PlayHit() { if (source != null && hit != null) source.PlayOneShot(hit); }

    static AudioClip BuildTone(string clipName, float duration, float startHz, float endHz, float volume)
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(rate * duration);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float phase = 2f * Mathf.PI * (startHz * t + (endHz - startHz) * t * t / (2f * duration));
            float envelope = Mathf.Pow(1f - i / (float)count, 2f);
            samples[i] = (Mathf.Sin(phase) * 0.76f + Mathf.Sin(phase * 2.01f) * 0.24f) * envelope * volume;
        }
        var clip = AudioClip.Create(clipName, count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    static AudioClip BuildHit()
    {
        const int rate = 22050;
        const float duration = 0.22f;
        int count = Mathf.CeilToInt(rate * duration);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float envelope = Mathf.Pow(1f - i / (float)count, 3f);
            float noise = Mathf.PerlinNoise(i * 0.037f, 0.41f) * 2f - 1f;
            samples[i] = (Mathf.Sin(2f * Mathf.PI * (115f - 42f * t) * t) * 0.55f + noise * 0.25f) * envelope * 0.4f;
        }
        var clip = AudioClip.Create("Boat Hit", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
