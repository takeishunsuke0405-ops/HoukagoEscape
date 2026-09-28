using UnityEngine;

/// <summary>
/// E で開け閉めするドア。教室は引き戸（Sliding）、それ以外は開き戸（Hinged）。
/// requiredItemId を入れると、その鍵を持っていないと開かない。
/// </summary>
public class Door : MonoBehaviour, IInteractable
{
    public enum DoorType { Sliding, Hinged }

    [SerializeField] string doorName = "ドア";
    [SerializeField] DoorType type = DoorType.Sliding;
    [Tooltip("動かす部分。開き戸の場合は、ちょうつがいの位置に原点がある空のオブジェクトにする")]
    [SerializeField] Transform panel;

    [Header("鍵")]
    [Tooltip("必要な鍵の itemId。空欄なら鍵なし")]
    [SerializeField] string requiredItemId;
    [SerializeField] string lockedMessage = "鍵がかかっている。";

    [Header("動き")]
    [SerializeField] float slideDistance = 0.95f;
    [SerializeField] float hingeAngle = 100f;
    [SerializeField] float openTime = 0.6f;
    [Tooltip("開け閉めの音が相沢に届く範囲 (m)")]
    [SerializeField] float noiseRadius = 8f;

    public bool IsLocked { get; private set; }
    public bool IsOpen { get; private set; }

    Vector3 closedPosition;
    Quaternion closedRotation;
    float openAmount;

    public string Prompt => IsOpen ? $"閉める：{doorName}" : $"開ける：{doorName}";
    public bool CanInteract => true;

    public void Setup(string name, DoorType doorType, Transform doorPanel, string keyId, string messageWhenLocked = "鍵がかかっている。")
    {
        doorName = name;
        type = doorType;
        panel = doorPanel;
        requiredItemId = keyId;
        lockedMessage = messageWhenLocked;
    }

    void Awake()
    {
        if (panel == null) panel = transform;
        closedPosition = panel.localPosition;
        closedRotation = panel.localRotation;
        IsLocked = !string.IsNullOrEmpty(requiredItemId);
    }

    public void Interact(Interactor interactor)
    {
        if (IsLocked)
        {
            var key = interactor.Inventory.Find(requiredItemId);
            if (key == null)
            {
                GameMessage.Show(lockedMessage);
                return;
            }
            IsLocked = false;
            GameMessage.Show($"{key.DisplayName}で鍵を開けた。");
        }
        IsOpen = !IsOpen;
        NoiseSystem.Emit(transform.position, noiseRadius);
    }

    void Update()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(openAmount, target)) return;

        openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime / openTime);
        float t = Mathf.SmoothStep(0f, 1f, openAmount);

        if (type == DoorType.Sliding)
            panel.localPosition = closedPosition + Vector3.right * (slideDistance * t);
        else
            panel.localRotation = closedRotation * Quaternion.Euler(0f, hingeAngle * t, 0f);
    }
}
