using UnityEngine;

/// <summary>
/// 拾えるアイテム。E で拾うと目の前に出てきて、よく見られる。
/// </summary>
public class ItemPickup : MonoBehaviour, IInteractable
{
    [Tooltip("鍵とドアを結びつけるための名前（半角英数）。例：key_2-3")]
    [SerializeField] string itemId = "item";
    [SerializeField] string displayName = "アイテム";
    [SerializeField, TextArea(2, 4)] string description;
    [Tooltip("手紙など、読める文章があるときだけ入力する")]
    [SerializeField, TextArea(3, 10)] string readText;
    [Tooltip("拾って閉じたあとに出す、主人公の独り言。空欄なら「〇〇を手に入れた。」")]
    [SerializeField, TextArea(1, 3)] string afterMessage;

    public string Prompt => $"拾う：{displayName}";
    public bool CanInteract => enabled;

    public void Setup(string id, string name, string itemDescription, string text = "", string monologue = "")
    {
        itemId = id;
        displayName = name;
        description = itemDescription;
        readText = text;
        afterMessage = monologue;
    }

    public void Interact(Interactor interactor) => interactor.PickUp(this);

    /// <summary>世界から取り除いて、所持品用のデータにする</summary>
    public InventoryItem TakeItem()
    {
        enabled = false;
        foreach (var itemCollider in GetComponentsInChildren<Collider>()) itemCollider.enabled = false;

        var item = new InventoryItem(itemId, displayName, description, readText, afterMessage, gameObject);
        FirstPersonLayer.Apply(gameObject);
        return item;
    }
}
