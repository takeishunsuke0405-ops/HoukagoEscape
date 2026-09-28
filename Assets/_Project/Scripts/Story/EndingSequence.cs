using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// エンディング（要件定義書「19. エンディング」）。
/// 校門を出る → 暗転 → 放課後の教室で目を開ける → 友人がいる → 窓の外には誰もいない
/// → 自分の机の上に相沢の手紙 →「見ていただけでも、同じだった。」→ END
/// </summary>
public class EndingSequence : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Camera playerCamera;
    [SerializeField] Inventory inventory;
    [Tooltip("目を開けたときに立っている場所（オープニングと同じ）")]
    [SerializeField] Transform wakeUpPoint;
    [SerializeField] GameObject[] friends;
    [SerializeField] GameObject chaser;
    [Tooltip("窓の外（校庭）を見たと判定する位置")]
    [SerializeField] Transform windowView;
    [Tooltip("主人公の机の上に置かれている手紙（最初は非表示）")]
    [SerializeField] GameObject letterOnDesk;

    const string LetterText = "今日も俺の机だけ離れていた。\n誰が動かしたのかは知ってる。\nでも、誰も元に戻さなかった。";
    const string FinalLine = "見ていただけでも、同じだった。";

    float blackout;
    string centerText;
    bool canRestart;
    GUIStyle centerStyle;
    GUIStyle hintStyle;

    public bool IsPlaying { get; private set; }

    void Start()
    {
        if (letterOnDesk != null) letterOnDesk.SetActive(false);
    }

    public void Play()
    {
        if (IsPlaying) return;
        IsPlaying = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 校門の外へ飛び出す → 暗転
        player.InputEnabled = false;
        chaser.SetActive(false);
        yield return FadeTo(1f, 0.6f);
        yield return new WaitForSeconds(2f);

        // 放課後の教室に戻っている
        inventory.Hold(-1);
        player.Teleport(wakeUpPoint.position, wakeUpPoint.eulerAngles.y);
        foreach (var friend in friends) friend.SetActive(true);
        letterOnDesk.SetActive(true);

        yield return FadeTo(0f, 2f);
        yield return Say("佐藤：「……おい、高橋。どうした？」", 3f);
        yield return Say("山本：「ぼーっとして。帰るぞ」", 2.5f);
        player.InputEnabled = true;

        GameMessage.Show("（……窓の外を見る）", 5f);
        yield return WaitUntilLookingAt(windowView.position, 25f, 1f);
        yield return Say("校庭には、誰もいない。", 3f);

        GameMessage.Show("（……机の上に、何か置いてある）", 5f);
        yield return WaitUntilLookingAt(letterOnDesk.transform.position, 12f, 1f);
        player.InputEnabled = false;
        yield return Say("ゲームの中で読んだはずの、相沢の手紙だった。", 3f);
        yield return Say(LetterText, 5f);

        // 最後の一文 → END
        yield return FadeTo(1f, 2f);
        centerText = FinalLine;
        yield return new WaitForSeconds(5f);
        centerText = "END";
        yield return new WaitForSeconds(2f);
        canRestart = true;
    }

    void Update()
    {
        if (!canRestart) return;
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool pressed = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        if (clicked || pressed) SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    IEnumerator FadeTo(float target, float seconds)
    {
        while (!Mathf.Approximately(blackout, target))
        {
            blackout = Mathf.MoveTowards(blackout, target, Time.deltaTime / seconds);
            yield return null;
        }
    }

    static IEnumerator Say(string text, float seconds)
    {
        GameMessage.Show(text, seconds);
        yield return new WaitForSeconds(seconds);
    }

    IEnumerator WaitUntilLookingAt(Vector3 point, float maxAngle, float holdSeconds)
    {
        float held = 0f;
        while (held < holdSeconds)
        {
            var cameraTransform = playerCamera.transform;
            bool looking = Vector3.Angle(cameraTransform.forward, point - cameraTransform.position) <= maxAngle;
            held = looking ? held + Time.deltaTime : 0f;
            yield return null;
        }
    }

    void OnGUI()
    {
        if (blackout <= 0f) return;
        GUI.depth = -60;

        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, blackout);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;

        if (string.IsNullOrEmpty(centerText)) return;
        centerStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), centerText, centerStyle);

        if (!canRestart) return;
        hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, Screen.height - 80f, Screen.width, 30f), "クリックで最初から", hintStyle);
    }
}
