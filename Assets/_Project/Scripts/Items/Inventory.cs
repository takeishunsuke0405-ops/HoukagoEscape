using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 所持アイテムと、今手に持っているアイテム。
/// マウスホイール・数字キー(1〜9)で持ち替え、Qでしまう（手ぶら）。
/// </summary>
public class Inventory : MonoBehaviour
{
    [SerializeField] PlayerController player;

    readonly List<InventoryItem> items = new();

    public IReadOnlyList<InventoryItem> Items => items;
    /// <summary>手に持っているアイテムの番号。-1 は手ぶら</summary>
    public int HeldIndex { get; private set; } = -1;
    public InventoryItem HeldItem => HeldIndex >= 0 && HeldIndex < items.Count ? items[HeldIndex] : null;

    /// <summary>アイテムが増えた、または持ち替えたとき</summary>
    public event Action Changed;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    public void Add(InventoryItem item)
    {
        items.Add(item);
        Changed?.Invoke();
    }

    public InventoryItem Find(string id) => items.Find(item => item.Id == id);
    public bool Has(string id) => Find(id) != null;

    public void Hold(InventoryItem item) => Hold(items.IndexOf(item));

    public void Hold(int index)
    {
        HeldIndex = Mathf.Clamp(index, -1, items.Count - 1);
        Changed?.Invoke();
    }

    void Update()
    {
        if (items.Count == 0) return;
        if (player != null && !player.InputEnabled) return;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0.1f) Cycle(-1);
        else if (scroll < -0.1f) Cycle(1);

        if (keyboard.qKey.wasPressedThisFrame) Hold(-1);

        for (int i = 0; i < 9 && i < items.Count; i++)
        {
            if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Hold(i);
        }
    }

    /// <summary>手ぶら → 1つ目 → 2つ目 … → 手ぶら の順に切り替える</summary>
    void Cycle(int direction)
    {
        int slots = items.Count + 1;
        int slot = (HeldIndex + 1 + direction + slots) % slots;
        Hold(slot - 1);
    }
}
