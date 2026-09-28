using UnityEngine;

/// <summary>
/// クライマックス（要件定義書「18. クライマックス」）。
/// 校門の鍵を持って体育館から校庭に出ると、背後の体育館から相沢が現れて、最後の追跡が始まる。
/// </summary>
public class ClimaxTrigger : PlayerZone
{
    [SerializeField] Inventory inventory;
    [SerializeField] string requiredItemId = "key_gate";
    [SerializeField] AizawaAI aizawa;
    [Tooltip("相沢が現れる場所（体育館の出口の内側）")]
    [SerializeField] Transform appearPoint;
    [Tooltip("最後の追跡の速さ。プレイヤーがスタミナを管理すれば、ぎりぎり逃げ切れるくらい")]
    [SerializeField] float climaxChaseSpeed = 3.8f;

    public void Setup(Inventory playerInventory, AizawaAI chaser, Transform appear)
    {
        inventory = playerInventory;
        aizawa = chaser;
        appearPoint = appear;
    }

    protected override bool CanTrigger() => inventory != null && inventory.Has(requiredItemId);

    protected override void OnPlayerEnter()
    {
        AudioSource.PlayClipAtPoint(ProceduralAudio.CreateThud("GymDoorBang", 0.6f, 6f, 0.25f, 51), appearPoint.position, 1f);
        aizawa.ForceChase(appearPoint.position, climaxChaseSpeed);
        GameMessage.Show("――背後で、体育館の扉が開く音がした。", 3f);
    }
}
