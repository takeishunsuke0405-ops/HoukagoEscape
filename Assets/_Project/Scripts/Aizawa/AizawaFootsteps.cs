using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 相沢の足音。歩いた距離に合わせて鳴らす。走るほど大きくなる。
/// 立体音響なので、どの方向から近づいてくるか音で分かる。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AizawaFootsteps : MonoBehaviour
{
    [SerializeField] NavMeshAgent agent;
    [Tooltip("1歩の長さ (m)")]
    [SerializeField] float strideLength = 0.75f;
    [SerializeField] float walkVolume = 0.6f;
    [SerializeField] float runVolume = 1f;
    [Tooltip("足音が聞こえる最大の距離 (m)")]
    [SerializeField] float hearingDistance = 25f;

    AudioSource source;
    AudioClip[] clips;
    float distanceSinceStep;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = hearingDistance;

        clips = new AudioClip[3];
        for (int i = 0; i < clips.Length; i++) clips[i] = ProceduralAudio.CreateThud($"AizawaStep{i}", seed: i + 1);
    }

    void Update()
    {
        if (!agent.enabled) return;
        float speed = agent.velocity.magnitude;
        if (speed < 0.1f) return;

        distanceSinceStep += speed * Time.deltaTime;
        if (distanceSinceStep < strideLength) return;
        distanceSinceStep = 0f;

        source.pitch = Random.Range(0.9f, 1.1f);
        float volume = Mathf.Lerp(walkVolume, runVolume, Mathf.InverseLerp(1.4f, 4.6f, speed));
        source.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }
}
