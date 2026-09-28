using System.Collections;
using UnityEngine;

/// <summary>
/// 校門の鍵。校門の鍵を持っていると E で開け始める。
/// 開けるまで数秒かかり、その間は動けない（背後から相沢が近づいてくる）。
/// </summary>
public class GateLock : MonoBehaviour, IInteractable
{
    [SerializeField] string requiredItemId = "key_gate";
    [Tooltip("横にスライドして開く門の板")]
    [SerializeField] Transform gatePanel;
    [Tooltip("鍵を開けるのにかかる時間（秒）")]
    [SerializeField] float unlockTime = 3f;
    [SerializeField] float slideDistance = 4.4f;

    public bool IsOpen { get; private set; }
    public bool IsUnlocking { get; private set; }

    public string Prompt => "開ける：校門";
    public bool CanInteract => !IsOpen && !IsUnlocking;

    float progress;
    PlayerController player;
    AudioSource source;
    AudioClip clickClip;
    GUIStyle labelStyle;

    public void Setup(Transform panel, string keyId)
    {
        gatePanel = panel;
        requiredItemId = keyId;
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        clickClip = ProceduralAudio.CreateThud("GateClick", 0.08f, 60f, 0.6f, 41);
    }

    public void Interact(Interactor interactor)
    {
        if (!interactor.Inventory.Has(requiredItemId))
        {
            GameMessage.Show("鍵がかかっている。校門の鍵が必要だ。");
            return;
        }
        player = interactor.GetComponent<PlayerController>();
        StartCoroutine(Unlock());
    }

    IEnumerator Unlock()
    {
        IsUnlocking = true;
        player.InputEnabled = false;
        GameMessage.Show("手が震えて、鍵がうまく回らない……", unlockTime);

        float nextClick = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime / unlockTime;
            nextClick -= Time.deltaTime;
            if (nextClick <= 0f)
            {
                source.PlayOneShot(clickClip, 0.8f);
                nextClick = Random.Range(0.25f, 0.6f);
            }
            yield return null;
        }

        IsUnlocking = false;
        IsOpen = true;
        player.InputEnabled = true;
        GameMessage.Show("開いた……！", 2f);

        Vector3 closed = gatePanel.position;
        Vector3 opened = closed + Vector3.right * slideDistance;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            gatePanel.position = Vector3.Lerp(closed, opened, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        gatePanel.position = opened;
    }

    void OnGUI()
    {
        if (!IsUnlocking) return;
        const float width = 240f;
        float x = (Screen.width - width) / 2f;
        float y = Screen.height / 2f + 40f;

        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, width, 8f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, width * progress, 8f), Texture2D.whiteTexture);
        GUI.color = previous;

        labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, y - 30f, Screen.width, 24f), "鍵を開けている……", labelStyle);
    }
}
