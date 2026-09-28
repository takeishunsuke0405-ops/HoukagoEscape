using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ロッカーなどに隠れる／出る。
/// 隠れている間は動けないが、マウスで少しだけ見回せる（扉の上のすき間からのぞく）。
/// </summary>
public class PlayerHiding : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [Tooltip("隠れている間に見回せる左右の角度")]
    [SerializeField] float maxYaw = 35f;
    [Tooltip("隠れている間に見回せる上下の角度")]
    [SerializeField] float maxPitch = 25f;
    [SerializeField] float lookSensitivity = 0.1f;
    [Tooltip("出入りするときの暗転の時間（秒）")]
    [SerializeField] float fadeTime = 0.25f;

    public HidingSpot CurrentSpot { get; private set; }
    public bool IsHidden => CurrentSpot != null;
    public bool IsTransitioning { get; private set; }

    float yaw;
    float pitch;
    float fade;
    float shakeTimer;
    Vector3 cameraBasePosition;
    GUIStyle hintStyle;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    public void Enter(HidingSpot spot)
    {
        if (IsHidden || IsTransitioning) return;
        StartCoroutine(EnterRoutine(spot));
    }

    IEnumerator EnterRoutine(HidingSpot spot)
    {
        IsTransitioning = true;
        player.InputEnabled = false;
        yield return FadeTo(1f);

        spot.PlayDoorSound();
        NoiseSystem.Emit(spot.transform.position, spot.NoiseRadius);
        player.SetHidden(true);
        player.Teleport(spot.HidePoint.position - Vector3.up * player.EyeHeight, spot.HidePoint.eulerAngles.y);
        cameraBasePosition = player.CameraRoot.localPosition;
        yaw = 0f;
        pitch = 0f;
        CurrentSpot = spot;
        spot.Banged += OnBanged;

        yield return FadeTo(0f);
        IsTransitioning = false;
    }

    IEnumerator ExitRoutine()
    {
        IsTransitioning = true;
        yield return FadeTo(1f);

        var spot = CurrentSpot;
        spot.Banged -= OnBanged;
        spot.PlayDoorSound();
        NoiseSystem.Emit(spot.transform.position, spot.NoiseRadius);
        player.CameraRoot.localPosition = cameraBasePosition;
        player.Teleport(spot.ExitPoint.position, spot.ExitPoint.eulerAngles.y);
        player.SetHidden(false);
        CurrentSpot = null;

        yield return FadeTo(0f);
        player.InputEnabled = true;
        IsTransitioning = false;
    }

    void Update()
    {
        if (!IsHidden || IsTransitioning) return;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        // すき間からのぞく：少しだけ見回せる
        Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
        yaw = Mathf.Clamp(yaw + delta.x, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        player.CameraRoot.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        // 叩かれたときの揺れ
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            player.CameraRoot.localPosition = cameraBasePosition + Random.insideUnitSphere * (0.02f * shakeTimer / 0.4f);
        }
        else
        {
            player.CameraRoot.localPosition = cameraBasePosition;
        }

        if (keyboard.eKey.wasPressedThisFrame) StartCoroutine(ExitRoutine());
    }

    void OnBanged() => shakeTimer = 0.4f;

    IEnumerator FadeTo(float target)
    {
        while (!Mathf.Approximately(fade, target))
        {
            fade = Mathf.MoveTowards(fade, target, Time.deltaTime / fadeTime);
            yield return null;
        }
    }

    void OnGUI()
    {
        GUI.depth = -40;
        if (fade > 0f)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        if (IsHidden && !IsTransitioning)
        {
            hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight };
            GUI.Label(new Rect(0f, 0f, Screen.width - 20f, Screen.height - 20f), "［E］出る　マウス：のぞく", hintStyle);
        }
    }
}
