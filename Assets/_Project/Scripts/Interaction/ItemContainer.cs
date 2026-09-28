using UnityEngine;

/// <summary>
/// 中にアイテムが入っているもの（机・引き出し・ロッカーなど）。
/// E で調べると、中のアイテムを拾ってよく見る状態になる。
/// </summary>
public class ItemContainer : MonoBehaviour, IInteractable
{
    [SerializeField] string containerName = "机";
    [Tooltip("中に入っているアイテム。再生中は見えないように隠しておく")]
    [SerializeField] ItemPickup containedItem;
    [SerializeField] string emptyMessage = "何も入っていない。";

    public string Prompt => $"調べる：{containerName}";
    public bool CanInteract => true;

    public void Setup(string name, ItemPickup item, string messageWhenEmpty = "何も入っていない。")
    {
        containerName = name;
        containedItem = item;
        emptyMessage = messageWhenEmpty;
    }

    void Awake()
    {
        if (containedItem != null) containedItem.gameObject.SetActive(false);
    }

    public void Interact(Interactor interactor)
    {
        if (containedItem == null)
        {
            GameMessage.Show(emptyMessage);
            return;
        }

        var item = containedItem;
        containedItem = null;
        item.gameObject.SetActive(true);
        interactor.PickUp(item);
    }
}
