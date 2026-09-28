using UnityEngine;

/// <summary>
/// E で見ると文章が出るもの（ポスター・黒板・シャッターなど）。
/// </summary>
public class InfoSign : MonoBehaviour, IInteractable
{
    [SerializeField] string signName = "ポスター";
    [Tooltip("案内に出る動詞（見る・調べる など）")]
    [SerializeField] string verb = "見る";
    [SerializeField, TextArea(2, 6)] string message;
    [SerializeField] float duration = 4f;

    public string Prompt => $"{verb}：{signName}";
    public bool CanInteract => true;

    public void Setup(string name, string text, string actionVerb = "見る")
    {
        signName = name;
        message = text;
        verb = actionVerb;
    }

    // 行数が多いほど長く表示する（1行につき2秒）
    public void Interact(Interactor interactor) => GameMessage.Show(message, Mathf.Max(duration, message.Split('\n').Length * 2f));
}
