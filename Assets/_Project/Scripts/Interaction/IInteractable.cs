/// <summary>
/// Eキーで調べられるもの（アイテム・机・ドアなど）。
/// </summary>
public interface IInteractable
{
    /// <summary>画面に出す案内（例：「拾う：2年3組の鍵」）</summary>
    string Prompt { get; }
    bool CanInteract { get; }
    void Interact(Interactor interactor);
}
