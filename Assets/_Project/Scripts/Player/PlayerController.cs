using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 一人称プレイヤーの移動・視点・ダッシュ・しゃがみ・ジャンプ・スタミナ。
/// 数値は docs/設計書_マップと進行Ver1.md の仮の値。Inspectorで調整する。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] Transform cameraRoot;

    [Header("移動の速さ (m/s)")]
    [SerializeField] float crouchSpeed = 1.5f;
    [SerializeField] float walkSpeed = 3.0f;
    [Tooltip("息切れ中（ゲージが赤いとき）の歩きの速さ。歩きより遅くする")]
    [SerializeField] float exhaustedSpeed = 1.5f;
    [SerializeField] float dashSpeed = 5.5f;

    [Header("ジャンプ・重力")]
    [SerializeField] float jumpHeight = 1.0f;
    [SerializeField] float gravity = -20f;

    [Header("しゃがみ")]
    [SerializeField] float standHeight = 1.7f;
    [SerializeField] float crouchHeight = 1.0f;
    [Tooltip("頭のてっぺんから目までの距離")]
    [SerializeField] float eyeOffset = 0.1f;
    [SerializeField] float crouchTransitionSpeed = 6f;

    [Header("スタミナ")]
    [Tooltip("満タンから0になるまでダッシュできる秒数")]
    [SerializeField] float dashDuration = 4f;
    [Tooltip("歩いているときに満タンまで回復する秒数")]
    [SerializeField] float walkRecoverTime = 6f;
    [Tooltip("止まっているときに満タンまで回復する秒数")]
    [SerializeField] float idleRecoverTime = 4f;
    [Tooltip("スタミナ切れのあと、再びダッシュできるようになる量")]
    [SerializeField, Range(0f, 1f)] float dashRestartThreshold = 0.25f;

    [Header("視点")]
    [SerializeField] float mouseSensitivity = 0.1f;
    [SerializeField] float maxPitch = 85f;

    [Header("物音の範囲 (m)")]
    [SerializeField] float crouchNoise = 0.5f;
    [SerializeField] float walkNoise = 3f;
    [SerializeField] float dashNoise = 12f;
    [SerializeField] float landNoise = 6f;

    /// <summary>スタミナ（0〜1）</summary>
    public float Stamina01 { get; private set; } = 1f;
    public bool IsDashing { get; private set; }
    public bool IsCrouching { get; private set; }
    /// <summary>スタミナ切れで、再びダッシュできる量まで回復していない状態</summary>
    public bool IsExhausted => exhausted;
    /// <summary>実際に動いている水平方向の速さ (m/s)。手の揺れに使う</summary>
    public float CurrentSpeed { get; private set; }
    public bool IsGrounded => controller.isGrounded;
    /// <summary>今プレイヤーが立てている物音の届く範囲。相沢のAIが参照する</summary>
    public float CurrentNoiseRadius { get; private set; }
    /// <summary>隠れているときやイベント中は false にして操作を止める</summary>
    public bool InputEnabled { get; set; } = true;
    /// <summary>ロッカーなどに隠れている（動かない・物音を立てない・当たり判定なし）</summary>
    public bool IsHidden { get; private set; }
    /// <summary>立っているときの、足元から目までの高さ</summary>
    public float EyeHeight => standHeight - eyeOffset;
    public Transform CameraRoot => cameraRoot;

    CharacterController controller;
    float pitch;
    float verticalVelocity;
    float currentHeight;
    bool exhausted;
    bool wasGrounded;
    float landNoiseTimer;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraRoot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraRoot = cam.transform;
        }
        currentHeight = standHeight;
        ApplyHeight();
    }

    void Start() => LockCursor(true);

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        HandleCursor(keyboard, mouse);

        if (IsHidden)
        {
            // 隠れている間は動かない。スタミナだけ回復する
            UpdateStamina(false);
            CurrentNoiseRadius = 0f;
            CurrentSpeed = 0f;
            return;
        }

        bool inputEnabled = InputEnabled;
        if (inputEnabled && Cursor.lockState == CursorLockMode.Locked) Look(mouse);

        Vector2 moveInput = inputEnabled ? ReadMove(keyboard) : Vector2.zero;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        UpdateCrouch(inputEnabled && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed));

        // ダッシュは Shift + 前進（W）のときだけ
        bool wantsDash = inputEnabled && keyboard.leftShiftKey.isPressed && moveInput.y > 0.1f;
        IsDashing = wantsDash && !IsCrouching && !exhausted && Stamina01 > 0f;

        UpdateStamina(isMoving);
        Move(moveInput, inputEnabled && keyboard.spaceKey.wasPressedThisFrame);
        UpdateNoise(isMoving);
    }

    /// <summary>隠れる／出る。隠れている間は当たり判定をなくす</summary>
    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        controller.enabled = !hidden;
        if (!hidden) return;

        IsDashing = false;
        IsCrouching = false;
        currentHeight = standHeight;
        ApplyHeight();
    }

    /// <summary>足元の位置と向きを指定して、瞬間移動する</summary>
    public void Teleport(Vector3 feetPosition, float yaw, float newPitch = 0f)
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(feetPosition, Quaternion.Euler(0f, yaw, 0f));
        pitch = newPitch;
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        verticalVelocity = 0f;
        controller.enabled = wasEnabled;
    }

    static Vector2 ReadMove(Keyboard keyboard)
    {
        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    void Look(Mouse mouse)
    {
        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move(Vector2 input, bool jumpPressed)
    {
        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (grounded && jumpPressed && !IsCrouching)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        float speed = IsDashing ? dashSpeed
            : IsCrouching ? crouchSpeed
            : exhausted ? exhaustedSpeed
            : walkSpeed;
        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        if (direction.sqrMagnitude > 1f) direction.Normalize();

        float fallSpeed = verticalVelocity;
        var flags = controller.Move((direction * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
        Vector3 velocity = controller.velocity;
        CurrentSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

        // 天井に頭をぶつけたら上昇をやめる
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

        // ある程度の速さで着地したら物音を立てる（階段を下りる程度では鳴らない）
        if (!wasGrounded && controller.isGrounded && fallSpeed < -4f) landNoiseTimer = 0.3f;
        wasGrounded = controller.isGrounded;
    }

    void UpdateCrouch(bool wantsCrouch)
    {
        if (wantsCrouch) IsCrouching = true;
        else if (IsCrouching && CanStandUp()) IsCrouching = false;

        float target = IsCrouching ? crouchHeight : standHeight;
        if (!Mathf.Approximately(currentHeight, target))
        {
            currentHeight = Mathf.MoveTowards(currentHeight, target, crouchTransitionSpeed * Time.deltaTime);
            ApplyHeight();
        }
    }

    /// <summary>頭の上に立ち上がれるだけの空間があるか（机や教卓の下で立てないようにする）</summary>
    bool CanStandUp()
    {
        float radius = controller.radius * 0.9f;
        Vector3 bottom = transform.position + Vector3.up * (radius + 0.05f);
        Vector3 top = transform.position + Vector3.up * (standHeight - radius);
        foreach (var hit in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit != controller) return false;
        }
        return true;
    }

    void ApplyHeight()
    {
        controller.height = currentHeight;
        controller.center = new Vector3(0f, currentHeight / 2f, 0f);
        if (cameraRoot != null) cameraRoot.localPosition = new Vector3(0f, currentHeight - eyeOffset, 0f);
    }

    void UpdateStamina(bool isMoving)
    {
        if (IsDashing)
        {
            Stamina01 = Mathf.Max(0f, Stamina01 - Time.deltaTime / dashDuration);
            if (Stamina01 <= 0f)
            {
                exhausted = true;
                IsDashing = false;
            }
        }
        else
        {
            float recoverTime = isMoving ? walkRecoverTime : idleRecoverTime;
            Stamina01 = Mathf.Min(1f, Stamina01 + Time.deltaTime / recoverTime);
            if (exhausted && Stamina01 >= dashRestartThreshold) exhausted = false;
        }
    }

    void UpdateNoise(bool isMoving)
    {
        float noise = 0f;
        if (controller.isGrounded && isMoving)
            noise = IsDashing ? dashNoise : IsCrouching ? crouchNoise : walkNoise;

        if (landNoiseTimer > 0f)
        {
            landNoiseTimer -= Time.deltaTime;
            noise = Mathf.Max(noise, landNoise);
        }
        CurrentNoiseRadius = noise;
    }

    void HandleCursor(Keyboard keyboard, Mouse mouse)
    {
        // Esc でカーソルを解放（後でメニューに置き換える）、クリックで再びロック
        if (keyboard.escapeKey.wasPressedThisFrame) LockCursor(false);
        else if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame) LockCursor(true);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void OnDrawGizmosSelected()
    {
        // 物音の範囲をシーンビューに表示する（プレイ中に Player を選択すると見える）
        if (CurrentNoiseRadius <= 0f) return;
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, CurrentNoiseRadius);
    }
}
