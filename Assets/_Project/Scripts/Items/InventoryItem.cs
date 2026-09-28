using UnityEngine;

/// <summary>
/// 所持しているアイテム1つ分。見た目は拾ったオブジェクトをそのまま使う。
/// </summary>
public class InventoryItem
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    /// <summary>手紙など、読める文章（なければ空）</summary>
    public string ReadText { get; }
    /// <summary>拾って閉じたあとに出す、主人公の独り言（なければ空）</summary>
    public string AfterMessage { get; }
    public GameObject Model { get; }

    readonly Vector3 baseScale;
    readonly float baseSize;

    public InventoryItem(string id, string displayName, string description, string readText, string afterMessage, GameObject model)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        ReadText = readText;
        AfterMessage = afterMessage;
        Model = model;
        baseScale = model.transform.lossyScale;
        baseSize = MeasureSize(model);
    }

    /// <summary>一番長い辺が size (m) になるように大きさをそろえる</summary>
    public void SetDisplaySize(float size)
    {
        Model.transform.localScale = baseScale * (size / baseSize);
    }

    static float MeasureSize(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 1f;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        return size > 0.0001f ? size : 1f;
    }
}
