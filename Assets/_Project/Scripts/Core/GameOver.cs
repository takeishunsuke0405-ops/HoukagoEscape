using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 相沢に捕まったときの演出。
/// 相沢の顔が一瞬だけ画面いっぱいに映る → 暗転 → GAME OVER → クリックでやり直し。
/// （チェックポイントができるまでは、シーンの最初からやり直す）
/// </summary>
public class GameOver : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Camera playerCamera;

    [Tooltip("顔が映っている時間（秒）")]
    [SerializeField] float faceTime = 0.6f;
    [Tooltip("顔を映すときの画角。小さいほど大きく映る")]
    [SerializeField] float faceFieldOfView = 18f;
    [Tooltip("暗転してから GAME OVER が出るまで（秒）")]
    [SerializeField] float textDelay = 1f;

    public bool IsGameOver { get; private set; }

    AudioSource audioSource;
    bool blackout;
    bool showText;
    bool canRestart;
    GUIStyle titleStyle;
    GUIStyle hintStyle;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    public void Trigger(Transform face)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        StartCoroutine(Sequence(face));
    }

    IEnumerator Sequence(Transform face)
    {
        player.InputEnabled = false;
        var hiding = player.GetComponent<PlayerHiding>();
        if (hiding != null) hiding.enabled = false;

        // 手を映すカメラを止めて、相沢の顔にカメラを向ける
        foreach (var childCamera in player.GetComponentsInChildren<Camera>())
            if (childCamera != playerCamera) childCamera.enabled = false;
        playerCamera.transform.LookAt(face.position);
        playerCamera.fieldOfView = faceFieldOfView;
        audioSource.PlayOneShot(ProceduralAudio.CreateThud("GameOverHit", 1.5f, 2.5f, 0.35f, 7));

        yield return new WaitForSeconds(faceTime);
        blackout = true;
        yield return new WaitForSeconds(textDelay);
        showText = true;
        yield return new WaitForSeconds(1f);
        canRestart = true;
    }

    void Update()
    {
        if (!IsGameOver) return;

        // よく見る画面を閉じたときなどに操作が戻らないようにする
        player.InputEnabled = false;

        if (!canRestart) return;
        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool pressed = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        if (clicked || pressed) SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    void OnGUI()
    {
        if (!blackout) return;
        GUI.depth = -100;

        var previous = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;

        if (!showText) return;
        titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.75f, 0.1f, 0.1f) } };
        hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };

        GUI.Label(new Rect(0f, Screen.height / 2f - 40f, Screen.width, 60f), "GAME OVER", titleStyle);
        if (canRestart) GUI.Label(new Rect(0f, Screen.height / 2f + 40f, Screen.width, 30f), "クリックでやり直す", hintStyle);
    }
}
