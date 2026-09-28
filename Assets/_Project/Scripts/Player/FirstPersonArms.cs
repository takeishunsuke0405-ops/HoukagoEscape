using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 画面下に映る両手の動き。
/// 歩く・走る・しゃがむときに腕を振り、マウスの動きに少し遅れてついてくる。
/// アイテムを持っているときは右手を前に出して、手の中にアイテムを表示する。
/// </summary>
public class FirstPersonArms : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Interactor interactor;
    [SerializeField] Inventory inventory;
    [SerializeField] Transform leftArm;
    [SerializeField] Transform rightArm;
    [Tooltip("右手の中でアイテムを置く位置")]
    [SerializeField] Transform handAnchor;

    [Header("腕の角度（度）。マイナスで上向き")]
    [SerializeField] float restPitch = -15f;
    [SerializeField] float holdPitch = -26f;
    [SerializeField] float holdYaw = -12f;

    [Header("腕の振り（度）")]
    [SerializeField] float crouchSwing = 5f;
    [SerializeField] float walkSwing = 10f;
    [SerializeField] float dashSwing = 25f;

    [Header("上下の揺れ (m)")]
    [SerializeField] float crouchBob = 0.006f;
    [SerializeField] float walkBob = 0.012f;
    [SerializeField] float dashBob = 0.03f;

    [Header("マウスに遅れてついてくる揺れ")]
    [SerializeField] float swayAmount = 0.04f;
    [SerializeField] float maxSway = 4f;

    [Header("手に持つアイテムの大きさ (m)")]
    [SerializeField] float heldSize = 0.12f;

    Vector3 basePosition;
    Renderer[] armRenderers;
    InventoryItem shownItem;
    bool visible = true;
    float phase;
    float breath;
    float swing;
    float bob;
    Vector2 sway;

    void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (interactor == null) interactor = GetComponentInParent<Interactor>();
        if (inventory == null) inventory = GetComponentInParent<Inventory>();
        basePosition = transform.localPosition;
        armRenderers = GetComponentsInChildren<Renderer>(true);
    }

    void OnEnable()
    {
        if (inventory != null) inventory.Changed += OnInventoryChanged;
    }

    void OnDisable()
    {
        if (inventory != null) inventory.Changed -= OnInventoryChanged;
    }

    void OnInventoryChanged() => ShowHeld(inventory.HeldItem);

    /// <summary>手に持つアイテムを表示する（null で手ぶら）</summary>
    public void ShowHeld(InventoryItem item)
    {
        if (shownItem != null && shownItem != item) shownItem.Model.SetActive(false);
        shownItem = item;
        if (item == null) return;

        var model = item.Model.transform;
        item.Model.SetActive(true);
        model.SetParent(handAnchor, false);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        item.SetDisplaySize(heldSize);
    }

    void Update()
    {
        // アイテムをよく見ている間と、隠れている間は手を隠す
        SetVisible((interactor == null || !interactor.IsInspecting) && !player.IsHidden);
        if (!visible) return;

        float dt = Time.deltaTime;
        float speed = player.CurrentSpeed;
        bool moving = speed > 0.1f && player.IsGrounded;

        float targetSwing = !moving ? 0f : player.IsDashing ? dashSwing : player.IsCrouching ? crouchSwing : walkSwing;
        float targetBob = !moving ? 0f : player.IsDashing ? dashBob : player.IsCrouching ? crouchBob : walkBob;
        float blend = 1f - Mathf.Exp(-10f * dt);
        swing = Mathf.Lerp(swing, targetSwing, blend);
        bob = Mathf.Lerp(bob, targetBob, blend);

        // 1歩で腕が半往復する。歩き約2歩/秒、ダッシュ約3歩/秒
        if (moving) phase += dt * (0.8f + speed * 0.4f) * Mathf.PI;
        breath += dt * 1.2f;
        float step = Mathf.Sin(phase);

        UpdateSway(dt);

        transform.localPosition = basePosition
            + new Vector3(Mathf.Cos(phase) * bob, -Mathf.Abs(step) * bob + Mathf.Sin(breath) * 0.003f, 0f);
        transform.localRotation = Quaternion.Euler(sway.y, -sway.x, 0f);

        // 左右の腕を交互に振る。アイテムを持っているときは右手を前に出す
        bool holding = shownItem != null;
        var leftTarget = Quaternion.Euler(restPitch + step * swing, 0f, 0f);
        var rightTarget = holding
            ? Quaternion.Euler(holdPitch + step * swing * 0.2f, holdYaw, 0f)
            : Quaternion.Euler(restPitch - step * swing, 0f, 0f);

        float armBlend = 1f - Mathf.Exp(-14f * dt);
        leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftTarget, armBlend);
        rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightTarget, armBlend);
    }

    void UpdateSway(float dt)
    {
        Vector2 target = Vector2.zero;
        var mouse = Mouse.current;
        if (mouse != null && player.InputEnabled && Cursor.lockState == CursorLockMode.Locked)
        {
            target = mouse.delta.ReadValue() * swayAmount;
            target = Vector2.ClampMagnitude(target, maxSway);
        }
        sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-8f * dt));
    }

    void SetVisible(bool value)
    {
        if (visible == value) return;
        visible = value;
        foreach (var armRenderer in armRenderers) armRenderer.enabled = value;
        handAnchor.gameObject.SetActive(value);
    }
}
