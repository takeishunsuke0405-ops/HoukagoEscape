using System.Collections;
using UnityEngine;

/// <summary>
/// オープニング（要件定義書「5. オープニング」）。ムービーではなく、プレイヤーが操作しながら進む。
///
/// 1. 友人と会話 → チャイム
/// 2. 窓の外を見る（校庭には誰もいない）
/// 3. 目を離した隙に友人が消える → 振り返ると誰もいない
/// 4. 遠くで「ガタン」
/// 5. 見ていない間に、校庭に相沢が現れる → 目が合う →「……相沢？」
/// 6. 照明が一瞬消える → 相沢が消えている → 校舎のどこかから足音 → 追跡者の相沢が動き出す
/// </summary>
public class OpeningSequence : MonoBehaviour
{
    [SerializeField] Camera playerCamera;
    [Tooltip("消える友人たち")]
    [SerializeField] GameObject[] friends;
    [Tooltip("窓の外（校庭）を見たと判定する位置")]
    [SerializeField] Transform windowView;
    [Tooltip("校庭に立っている相沢（演出用。追いかけてこない）")]
    [SerializeField] GameObject yardAizawa;
    [Tooltip("オープニングの後に動き出す、追いかけてくる相沢")]
    [SerializeField] GameObject chaser;
    [Tooltip("一瞬消える照明（太陽・教室の蛍光灯）")]
    [SerializeField] Light[] flickerLights;
    [Tooltip("「ガタン」が鳴る場所")]
    [SerializeField] Transform bangPoint;
    [Tooltip("足音が近づいてくる経路（始まり → 終わり）")]
    [SerializeField] Transform footstepsFrom;
    [SerializeField] Transform footstepsTo;

    bool blackout;

    void Start()
    {
        if (yardAizawa != null) yardAizawa.SetActive(false);
        if (chaser != null) chaser.SetActive(false);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(1f);
        yield return Say("佐藤：「そろそろ帰るか。」", 2.5f);
        yield return Say("山本：「帰りにコンビニ寄ってこうぜ。」", 2.5f);
        yield return Say("高橋：「そうだな。」", 2f);

        // 1. チャイム
        var chime = gameObject.AddComponent<AudioSource>();
        chime.spatialBlend = 0f;
        chime.volume = 0.6f;
        chime.PlayOneShot(ProceduralAudio.CreateChime("Chime"));
        GameMessage.Show("キーン　コーン　カーン　コーン……", 4f);
        yield return new WaitForSeconds(3f);

        // 2. 窓の外を見る
        GameMessage.Show("（……窓の外が、やけに赤い）", 5f);
        yield return WaitUntilLookingAt(windowView.position, 25f, 1f);
        GameMessage.Show("夕日に照らされた校庭。誰もいない。", 3f);

        // 3. 目を離した隙に、友人が消える
        yield return new WaitUntil(() => !AnyOnScreen(friends));
        foreach (var friend in friends) friend.SetActive(false);

        yield return WaitUntilLookingAt(FriendsCenter(), 35f, 0.3f);
        yield return new WaitForSeconds(0.5f);
        yield return Say("……？", 2f);
        yield return Say("高橋：「おい？」", 2.5f);
        yield return Say("返事はない。机の上に、佐藤のスマートフォンだけが残されている。", 3.5f);

        // 4. 遠くで物音
        AudioSource.PlayClipAtPoint(ProceduralAudio.CreateThud("Bang", 0.6f, 6f, 0.25f, 11), bangPoint.position, 1f);
        yield return Say("――ガタン……", 3f);

        // 5. 見ていない間に、校庭に相沢が立っている
        Vector3 aizawaHead = yardAizawa.transform.position + Vector3.up * 1.6f;
        yield return new WaitUntil(() => !IsOnScreen(aizawaHead));
        yardAizawa.SetActive(true);
        GameMessage.Show("（窓の外……誰か、いる？）", 5f);

        yield return WaitUntilLookingAt(aizawaHead, 6f, 1.5f, true);
        yield return Say("夕日の逆光で、顔はよく見えない。でも、こっちを見ている。", 3f);
        yield return Say("高橋：「……相沢？」", 3f);
        yield return new WaitForSeconds(1.5f);

        // 6. 照明が一瞬消える → 相沢がいなくなっている
        yield return LightsFlicker();
        yield return new WaitForSeconds(1f);
        yield return Say("（……いない）", 2.5f);

        StartCoroutine(DistantFootsteps());
        yield return Say("校舎のどこかから、足音が聞こえる。", 3.5f);

        chaser.SetActive(true);
        GameMessage.Show("（ここから出ないと……）", 4f);
    }

    // ───────────── 演出 ─────────────

    IEnumerator LightsFlicker()
    {
        var intensities = new float[flickerLights.Length];
        for (int i = 0; i < flickerLights.Length; i++) intensities[i] = flickerLights[i].intensity;
        float ambient = RenderSettings.ambientIntensity;

        // パッ、パッ……と2回瞬いてから消える
        float[] pattern = { 0.08f, 0.1f, 0.06f, 0.12f };
        for (int i = 0; i < pattern.Length; i++)
        {
            SetLights(i % 2 == 0 ? 0f : 1f, intensities, ambient);
            yield return new WaitForSeconds(pattern[i]);
        }

        SetLights(0f, intensities, ambient);
        yardAizawa.SetActive(false);
        yield return new WaitForSeconds(0.9f);
        SetLights(1f, intensities, ambient);
    }

    void SetLights(float scale, float[] intensities, float ambient)
    {
        for (int i = 0; i < flickerLights.Length; i++) flickerLights[i].intensity = intensities[i] * scale;
        RenderSettings.ambientIntensity = ambient * scale;
        blackout = scale == 0f;
    }

    /// <summary>遠くの廊下を、足音がゆっくり歩いていく</summary>
    IEnumerator DistantFootsteps()
    {
        var walker = new GameObject("DistantFootsteps");
        var source = walker.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 40f;
        var step = ProceduralAudio.CreateThud("DistantStep", seed: 5);

        const int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            walker.transform.position = Vector3.Lerp(footstepsFrom.position, footstepsTo.position, i / (float)(steps - 1));
            source.pitch = Random.Range(0.9f, 1.05f);
            source.PlayOneShot(step, 1f);
            yield return new WaitForSeconds(0.6f);
        }
        Destroy(walker, 1f);
    }

    void OnGUI()
    {
        if (!blackout) return;
        GUI.depth = -50;
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // ───────────── 見ているかどうか ─────────────

    static IEnumerator Say(string text, float seconds)
    {
        GameMessage.Show(text, seconds);
        yield return new WaitForSeconds(seconds);
    }

    /// <summary>
    /// 画面の中央から maxAngle 度以内に point が映っている状態が holdSeconds 秒続くまで待つ。
    /// requireLineOfSight が true のときは、壁ごしでは見たことにしない。
    /// </summary>
    IEnumerator WaitUntilLookingAt(Vector3 point, float maxAngle, float holdSeconds, bool requireLineOfSight = false)
    {
        float held = 0f;
        while (held < holdSeconds)
        {
            var cameraTransform = playerCamera.transform;
            bool looking = Vector3.Angle(cameraTransform.forward, point - cameraTransform.position) <= maxAngle;
            if (looking && requireLineOfSight)
                looking = !Physics.Linecast(cameraTransform.position, point, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            held = looking ? held + Time.deltaTime : 0f;
            yield return null;
        }
    }

    bool IsOnScreen(Vector3 point)
    {
        Vector3 viewport = playerCamera.WorldToViewportPoint(point);
        return viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
    }

    bool AnyOnScreen(GameObject[] targets)
    {
        foreach (var target in targets)
        {
            Vector3 feet = target.transform.position;
            if (IsOnScreen(feet) || IsOnScreen(feet + Vector3.up * 0.9f) || IsOnScreen(feet + Vector3.up * 1.6f)) return true;
        }
        return false;
    }

    Vector3 FriendsCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (var friend in friends) sum += friend.transform.position;
        return sum / friends.Length + Vector3.up * 1.2f;
    }
}
