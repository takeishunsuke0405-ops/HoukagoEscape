using UnityEngine;

/// <summary>
/// 画面下に短く表示するメッセージ（「鍵がかかっている。」や主人公の独り言など）。
/// </summary>
public static class GameMessage
{
    public static string Text { get; private set; }
    public static bool IsVisible => Text != null && Time.time < hideTime;

    static float hideTime;

    public static void Show(string text, float duration = 3f)
    {
        Text = text;
        hideTime = Time.time + duration;
    }

    // ドメインリロードなしで再生しても前回のメッセージが残らないようにする
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Text = null;
        hideTime = 0f;
    }
}
