using System;
using UnityEngine;

/// <summary>
/// 隠れられる場所（掃除用具ロッカーなど）。E で中に入る。
/// 中からは扉の上のすき間（通気口）から外をのぞける。
/// </summary>
public class HidingSpot : MonoBehaviour, IInteractable
{
    [SerializeField] string spotName = "掃除用具ロッカー";
    [Tooltip("中に入ったときの目の位置と向き")]
    [SerializeField] Transform hidePoint;
    [Tooltip("出たときに立つ位置と向き")]
    [SerializeField] Transform exitPoint;
    [Tooltip("出入りするときの扉の音が相沢に届く範囲 (m)")]
    [SerializeField] float noiseRadius = 4f;

    public Transform HidePoint => hidePoint;
    public Transform ExitPoint => exitPoint;
    public float NoiseRadius => noiseRadius;
    public string Prompt => $"隠れる：{spotName}";
    public bool CanInteract => true;

    /// <summary>相沢に外から叩かれたとき</summary>
    public event Action Banged;

    static AudioClip doorClip;
    static AudioClip bangClip;
    AudioSource source;

    public void Setup(string name, Transform hide, Transform exit)
    {
        spotName = name;
        hidePoint = hide;
        exitPoint = exit;
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 20f;

        if (doorClip == null) doorClip = ProceduralAudio.CreateThud("LockerDoor", 0.25f, 18f, 0.4f, 21);
        if (bangClip == null) bangClip = ProceduralAudio.CreateThud("LockerBang", 0.5f, 9f, 0.3f, 31);
    }

    public void Interact(Interactor interactor)
    {
        var hiding = interactor.GetComponent<PlayerHiding>();
        if (hiding != null) hiding.Enter(this);
    }

    public void PlayDoorSound() => source.PlayOneShot(doorClip, 0.7f);

    /// <summary>外から「ドン」と叩く</summary>
    public void Bang()
    {
        source.PlayOneShot(bangClip, 1f);
        Banged?.Invoke();
    }
}
