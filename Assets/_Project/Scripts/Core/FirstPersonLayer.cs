using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 手やよく見ているアイテムを描くレイヤー。
/// 専用のカメラで一番手前に描くので、壁に近づいても手がめり込まない。
/// レイヤー自体はメニュー「放課後 > プレイヤーのテストシーンを作成」で自動追加される。
/// </summary>
public static class FirstPersonLayer
{
    public const string Name = "FirstPerson";

    public static void Apply(GameObject target)
    {
        int layer = LayerMask.NameToLayer(Name);
        if (layer < 0) return;

        foreach (var child in target.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
}
