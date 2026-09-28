using UnityEngine;

/// <summary>
/// プレイヤーが決まった範囲に入ったら、一度だけ何かを起こす。
/// 範囲はこのオブジェクトを中心とした箱（size）。
/// </summary>
public abstract class PlayerZone : MonoBehaviour
{
    [SerializeField] protected PlayerController player;
    [SerializeField] Vector3 size = new(4f, 3f, 4f);

    bool triggered;

    public void SetupZone(PlayerController target, Vector3 zoneSize)
    {
        player = target;
        size = zoneSize;
    }

    void Update()
    {
        if (triggered || player == null) return;

        Vector3 local = transform.InverseTransformPoint(player.transform.position + Vector3.up * 0.5f);
        bool inside = Mathf.Abs(local.x) <= size.x / 2f && Mathf.Abs(local.y) <= size.y / 2f && Mathf.Abs(local.z) <= size.z / 2f;
        if (inside && CanTrigger())
        {
            triggered = true;
            OnPlayerEnter();
        }
    }

    protected virtual bool CanTrigger() => true;
    protected abstract void OnPlayerEnter();

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
