using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プロトタイプ用の簡易HUD。
/// スタミナゲージ・画面中央の点・調べる案内・メッセージ・よく見る画面・所持品一覧(Tab)・デバッグ表示。
/// 正式なUIは後でCanvasで作り直す。
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Interactor interactor;
    [SerializeField] Inventory inventory;
    [SerializeField] bool showDebug = true;

    GUIStyle debugStyle;
    GUIStyle centerStyle;
    GUIStyle titleStyle;
    GUIStyle bodyStyle;
    GUIStyle smallRightStyle;

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (interactor == null) interactor = GetComponent<Interactor>();
        if (inventory == null) inventory = GetComponent<Inventory>();
    }

    void OnGUI()
    {
        if (player == null) return;
        CreateStyles();

        if (interactor != null && interactor.IsInspecting)
        {
            DrawInspect(interactor.InspectingItem);
            DrawMessage();
            return;
        }

        if (player.IsHidden)
        {
            // 隠れている間は、すき間からの景色だけを見せる
            DrawMessage();
            return;
        }

        DrawCrosshair();
        DrawPrompt();
        DrawStamina();
        DrawHeldItem();
        DrawMessage();
        if (Keyboard.current != null && Keyboard.current.tabKey.isPressed) DrawInventory();
        if (showDebug) DrawDebug();
    }

    void CreateStyles()
    {
        if (debugStyle != null) return;
        debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
        smallRightStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.LowerRight };
    }

    static void DrawCrosshair()
    {
        const float size = 4f;
        DrawRect(new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size), new Color(1f, 1f, 1f, 0.6f));
    }

    void DrawPrompt()
    {
        if (interactor == null || interactor.Focused == null) return;
        var rect = new Rect(0f, Screen.height / 2f + 20f, Screen.width, 30f);
        DrawShadowLabel(rect, $"［E］{interactor.Focused.Prompt}", centerStyle);
    }

    void DrawStamina()
    {
        // 満タンのときは表示しない
        if (player.Stamina01 >= 1f) return;

        const float width = 240f;
        const float height = 6f;
        float x = (Screen.width - width) / 2f;
        float y = Screen.height - 60f;
        DrawRect(new Rect(x, y, width, height), new Color(0f, 0f, 0f, 0.5f));

        Color fill = player.IsExhausted ? new Color(0.8f, 0.25f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
        DrawRect(new Rect(x, y, width * player.Stamina01, height), fill);
    }

    void DrawHeldItem()
    {
        if (inventory == null || inventory.HeldItem == null) return;
        var rect = new Rect(0f, 0f, Screen.width - 20f, Screen.height - 20f);
        DrawShadowLabel(rect,
            $"手に持っている：{inventory.HeldItem.DisplayName}\n［右クリック］よく見る　［ホイール／1〜9］持ち替え　［Q］しまう",
            smallRightStyle);
    }

    void DrawMessage()
    {
        if (!GameMessage.IsVisible) return;

        // 行数に合わせて高さを変え、下端をそろえる（長い文章でも途切れないようにする）
        float height = centerStyle.CalcHeight(new GUIContent(GameMessage.Text), Screen.width) + 10f;
        DrawShadowLabel(new Rect(0f, Screen.height - 90f - height, Screen.width, height), GameMessage.Text, centerStyle);
    }

    void DrawInspect(InventoryItem item)
    {
        DrawShadowLabel(new Rect(0f, 30f, Screen.width, 40f), item.DisplayName, titleStyle);

        if (!string.IsNullOrEmpty(item.Description))
            DrawShadowLabel(new Rect(0f, 75f, Screen.width, 30f), item.Description, centerStyle);

        // 手紙などの文章は右側に表示する
        if (!string.IsNullOrEmpty(item.ReadText))
        {
            var panel = new Rect(Screen.width * 0.62f, Screen.height * 0.2f, Screen.width * 0.34f, Screen.height * 0.55f);
            DrawRect(panel, new Color(0f, 0f, 0f, 0.7f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 20f, panel.width - 40f, panel.height - 40f), item.ReadText, bodyStyle);
        }

        DrawShadowLabel(new Rect(0f, Screen.height - 50f, Screen.width, 30f), "マウス：回す　　［E］／［右クリック］閉じる", centerStyle);
    }

    void DrawInventory()
    {
        var panel = new Rect(Screen.width - 340f, 20f, 320f, 50f + 30f * Mathf.Max(1, inventory.Items.Count));
        DrawRect(panel, new Color(0f, 0f, 0f, 0.7f));
        GUI.Label(new Rect(panel.x + 15f, panel.y + 10f, panel.width, 30f), "持ち物", debugStyle);

        if (inventory.Items.Count == 0)
        {
            GUI.Label(new Rect(panel.x + 15f, panel.y + 40f, panel.width, 30f), "何も持っていない", debugStyle);
            return;
        }

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            string mark = i == inventory.HeldIndex ? "▶ " : "　 ";
            GUI.Label(new Rect(panel.x + 15f, panel.y + 40f + 30f * i, panel.width, 30f),
                $"{mark}{i + 1}. {inventory.Items[i].DisplayName}", debugStyle);
        }
    }

    void DrawDebug()
    {
        string state = player.IsDashing ? "ダッシュ" : player.IsCrouching ? "しゃがみ" : "通常";
        string exhausted = player.IsExhausted ? "（息切れ）" : "";
        GUI.Label(new Rect(10f, 10f, 400f, 80f),
            $"状態: {state}\nスタミナ: {player.Stamina01 * 100f:0}%{exhausted}\n物音の範囲: {player.CurrentNoiseRadius:0.0}m",
            debugStyle);
    }

    static void DrawShadowLabel(Rect rect, string text, GUIStyle style)
    {
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        GUI.color = previous;
        GUI.Label(rect, text, style);
    }

    static void DrawRect(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
