using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 画面中央にあるものを E で調べる。アイテムを拾ったときは目の前に出して「よく見る」状態にする。
/// よく見ている間はマウスでアイテムを回せる。E か右クリックで閉じると手に持つ。
/// 手に持っているアイテムは右クリックでもう一度よく見られる。
/// </summary>
public class Interactor : MonoBehaviour
{
    [SerializeField] Camera playerCamera;
    [SerializeField] PlayerController player;
    [SerializeField] Inventory inventory;
    [SerializeField] FirstPersonArms arms;

    [Header("調べる")]
    [Tooltip("調べられる距離 (m)")]
    [SerializeField] float reach = 2.2f;

    [Header("よく見る")]
    [SerializeField] float inspectDistance = 0.45f;
    [Tooltip("よく見るときのアイテムの大きさ (m)")]
    [SerializeField] float inspectSize = 0.22f;
    [SerializeField] float inspectRotateSpeed = 0.3f;

    /// <summary>今画面中央にある、調べられるもの</summary>
    public IInteractable Focused { get; private set; }
    public InventoryItem InspectingItem { get; private set; }
    public bool IsInspecting => InspectingItem != null;
    public Inventory Inventory => inventory;

    Transform inspectAnchor;
    bool addToInventoryOnClose;
    int inspectStartFrame;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (inventory == null) inventory = GetComponent<Inventory>();
        if (arms == null) arms = GetComponentInChildren<FirstPersonArms>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

        inspectAnchor = new GameObject("InspectAnchor").transform;
        inspectAnchor.SetParent(playerCamera.transform, false);
        inspectAnchor.localPosition = new Vector3(0f, 0f, inspectDistance);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (IsInspecting)
        {
            UpdateInspect(keyboard, mouse);
            return;
        }

        Focused = FindFocused();
        if (!player.InputEnabled) return;

        if (keyboard.eKey.wasPressedThisFrame && Focused != null)
            Focused.Interact(this);
        else if (mouse.rightButton.wasPressedThisFrame && inventory.HeldItem != null)
            StartInspect(inventory.HeldItem, false);
    }

    IInteractable FindFocused()
    {
        var cameraTransform = playerCamera.transform;
        var ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (!Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore)) return null;

        var interactable = hit.collider.GetComponentInParent<IInteractable>();
        return interactable != null && interactable.CanInteract ? interactable : null;
    }

    /// <summary>アイテムを拾って、よく見る状態にする。閉じると所持品に入る</summary>
    public void PickUp(ItemPickup pickup)
    {
        StartInspect(pickup.TakeItem(), true);
    }

    void StartInspect(InventoryItem item, bool isNewItem)
    {
        InspectingItem = item;
        addToInventoryOnClose = isNewItem;
        inspectStartFrame = Time.frameCount;
        player.InputEnabled = false;

        var model = item.Model.transform;
        item.Model.SetActive(true);
        model.SetParent(inspectAnchor, false);
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        item.SetDisplaySize(inspectSize);
    }

    void UpdateInspect(Keyboard keyboard, Mouse mouse)
    {
        // マウスの動きでアイテムを回す
        var cameraTransform = playerCamera.transform;
        var model = InspectingItem.Model.transform;
        Vector2 delta = mouse.delta.ReadValue() * inspectRotateSpeed;
        model.Rotate(cameraTransform.up, -delta.x, Space.World);
        model.Rotate(cameraTransform.right, delta.y, Space.World);

        // 開いたときと同じフレームのEで閉じないようにする
        if (Time.frameCount == inspectStartFrame) return;
        if (keyboard.eKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) EndInspect();
    }

    void EndInspect()
    {
        var item = InspectingItem;
        InspectingItem = null;
        player.InputEnabled = true;

        if (addToInventoryOnClose)
        {
            inventory.Add(item);
            inventory.Hold(item);
            if (string.IsNullOrEmpty(item.AfterMessage)) GameMessage.Show($"{item.DisplayName}を手に入れた。");
            else GameMessage.Show(item.AfterMessage, 5f);
        }

        if (arms != null) arms.ShowHeld(inventory.HeldItem);
        else item.Model.SetActive(false);
    }
}
