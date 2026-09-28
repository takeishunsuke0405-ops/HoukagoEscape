using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 相沢の行動。徘徊 → 警戒（怪しむ）→ 発見 → 追跡 → 探索 の5つの状態を切り替える。
/// 数値は docs/設計書_マップと進行Ver1.md「4. 相沢の仕様」の仮の値。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AizawaAI : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Spotted, Chase, Search, Caught }

    [Header("参照")]
    [SerializeField] PlayerController player;
    [Tooltip("プレイヤーの目（カメラ）")]
    [SerializeField] Transform playerEyes;
    [Tooltip("相沢の目の位置")]
    [SerializeField] Transform eyes;
    [Tooltip("捕まったときに画面いっぱいに映す顔")]
    [SerializeField] Transform face;
    [SerializeField] GameOver gameOver;
    [SerializeField] Transform[] patrolPoints;

    [Header("見つかる条件")]
    [SerializeField] float sightDistance = 16f;
    [SerializeField] float sightAngle = 110f;
    [Tooltip("プレイヤーの画面の中央からこの角度以内に相沢が映っていたら「見ている」")]
    [SerializeField] float playerLookAngle = 20f;
    [Tooltip("この距離以内で相沢の視界に入ると、目が合っていなくても見つかる。追跡中はこの距離で掴みかかる")]
    [SerializeField] float closeDetectDistance = 4f;

    [Header("速さ (m/s)")]
    [SerializeField] float patrolSpeed = 1.4f;
    [SerializeField] float suspiciousSpeed = 2f;
    [SerializeField] float chaseSpeed = 4.6f;
    [SerializeField] float lungeSpeed = 6f;
    [SerializeField] float searchSpeed = 2f;

    [Header("時間（秒）")]
    [SerializeField] float patrolWaitTime = 2f;
    [SerializeField] float suspiciousLookTime = 8f;
    [SerializeField] float spottedPauseTime = 1f;
    [SerializeField] float loseSightTime = 3f;
    [SerializeField] float searchTime = 15f;

    [Header("隠れているプレイヤー")]
    [Tooltip("ロッカーのこの距離まで近づいたら、叩くかどうか決める (m)")]
    [SerializeField] float bangDistance = 2.5f;
    [Tooltip("近くを通ったときに、ロッカーを叩く確率（0〜1）")]
    [SerializeField, Range(0f, 1f)] float bangChance = 0.5f;

    [Header("その他")]
    [SerializeField] float catchDistance = 1f;
    [SerializeField] float searchRadius = 6f;
    [SerializeField] bool showDebug = true;

    public State CurrentState { get; private set; } = State.Patrol;

    NavMeshAgent agent;
    PlayerHiding playerHiding;
    bool wasPlayerHidden;
    bool sawPlayerHide;
    bool bangDecided;
    bool isBanging;
    bool forcedChase;
    int sightMask;
    int patrolIndex;
    float stateTimer;
    float waitTimer;
    float lookTimer;
    float lostSightTimer;
    float distanceToPlayer;
    Vector3 lastKnownPosition;
    GUIStyle debugStyle;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        playerHiding = player.GetComponent<PlayerHiding>();
        if (eyes == null) eyes = transform;
        if (face == null) face = eyes;

        // 手に持っているアイテムなどは視線をさえぎらない
        sightMask = ~0;
        int firstPersonLayer = LayerMask.NameToLayer(FirstPersonLayer.Name);
        if (firstPersonLayer >= 0) sightMask &= ~(1 << firstPersonLayer);
    }

    void OnEnable() => NoiseSystem.NoiseMade += OnNoise;
    void OnDisable() => NoiseSystem.NoiseMade -= OnNoise;

    void Start()
    {
        // ナビメッシュができてから動き始める（最初は NavMeshAgent を無効にしてある）
        agent.enabled = true;
        agent.Warp(transform.position);
        if (!forcedChase) SetState(State.Patrol);
    }

    void Update()
    {
        if (CurrentState == State.Caught || !agent.isOnNavMesh || isBanging) return;

        float dt = Time.deltaTime;
        stateTimer += dt;
        distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        if (UpdateHiddenPlayer(dt)) return;
        bool sees = CanSeePlayer();
        bool eyeContact = sees && IsPlayerLookingAtMe();

        // プレイヤーの足音が届いたら怪しむ
        if (player.CurrentNoiseRadius > 0f) OnNoise(player.transform.position, player.CurrentNoiseRadius);

        switch (CurrentState)
        {
            case State.Patrol:
            case State.Suspicious:
                if (sees && (eyeContact || distanceToPlayer <= closeDetectDistance))
                {
                    SetState(State.Spotted);
                    break;
                }
                // 相沢からは見えているが、目は合っていない → 近づいて確かめに来る
                if (sees) Investigate(player.transform.position);

                if (CurrentState == State.Patrol) UpdatePatrol(dt);
                else UpdateSuspicious(dt);
                break;

            case State.Spotted:
                FaceTowards(player.transform.position, dt);
                if (stateTimer >= spottedPauseTime) SetState(State.Chase);
                break;

            case State.Chase:
                UpdateChase(sees, dt);
                break;

            case State.Search:
                if (sees) SetState(State.Chase);
                else UpdateSearch();
                break;
        }
    }

    void SetState(State next)
    {
        CurrentState = next;
        stateTimer = 0f;
        agent.isStopped = next == State.Spotted || next == State.Caught;

        switch (next)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                waitTimer = 0f;
                GoToPatrolPoint();
                break;
            case State.Suspicious:
                agent.speed = suspiciousSpeed;
                lookTimer = 0f;
                break;
            case State.Spotted:
                agent.ResetPath();
                break;
            case State.Chase:
                lostSightTimer = 0f;
                lastKnownPosition = player.transform.position;
                break;
            case State.Search:
                agent.speed = searchSpeed;
                agent.SetDestination(lastKnownPosition);
                break;
            case State.Caught:
                agent.ResetPath();
                if (gameOver != null) gameOver.Trigger(face);
                break;
        }
    }

    /// <summary>
    /// 決まった場所に現れて、すぐに追いかけ始める（クライマックス用）。
    /// speed が 0 より大きいときは、追跡の速さをその値にする。
    /// </summary>
    public void ForceChase(Vector3 position, float speed)
    {
        forcedChase = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (!agent.enabled) agent.enabled = true;
        agent.Warp(position);
        if (speed > 0f) chaseSpeed = speed;
        FaceTowards(player.transform.position, 1f);
        SetState(State.Chase);
    }

    // ───────────── 各状態 ─────────────

    void UpdatePatrol(float dt)
    {
        if (patrolPoints == null || patrolPoints.Length == 0 || !HasArrived()) return;

        waitTimer += dt;
        if (waitTimer < patrolWaitTime) return;

        waitTimer = 0f;
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        GoToPatrolPoint();
    }

    void GoToPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        agent.SetDestination(patrolPoints[patrolIndex].position);
    }

    /// <summary>気になる場所へ見に行く</summary>
    void Investigate(Vector3 position)
    {
        if (CurrentState != State.Suspicious) SetState(State.Suspicious);
        lookTimer = 0f;
        agent.SetDestination(position);
    }

    void UpdateSuspicious(float dt)
    {
        if (!HasArrived()) return;

        // 着いたら、その場で見回す
        lookTimer += dt;
        transform.Rotate(0f, Mathf.Sin(lookTimer * 1.5f) * 90f * dt, 0f);
        if (lookTimer >= suspiciousLookTime) SetState(State.Patrol);
    }

    void UpdateChase(bool sees, float dt)
    {
        if (sees)
        {
            lostSightTimer = 0f;
            lastKnownPosition = player.transform.position;
        }
        else
        {
            lostSightTimer += dt;
        }

        // 見えなくなったら、最後に見た場所へ向かう
        agent.speed = distanceToPlayer <= closeDetectDistance ? lungeSpeed : chaseSpeed;
        agent.SetDestination(lastKnownPosition);

        bool hidden = playerHiding != null && playerHiding.IsHidden;
        if (distanceToPlayer <= catchDistance && !hidden) SetState(State.Caught);
        else if (lostSightTimer >= loseSightTime) SetState(State.Search);
    }

    void UpdateSearch()
    {
        if (stateTimer >= searchTime)
        {
            SetState(State.Patrol);
            return;
        }
        if (!HasArrived()) return;

        // 見失った場所の周りをうろうろ探す
        Vector3 candidate = lastKnownPosition + Random.insideUnitSphere * searchRadius;
        candidate.y = lastKnownPosition.y;
        if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) agent.SetDestination(hit.position);
    }

    void OnNoise(Vector3 position, float radius)
    {
        if (CurrentState == State.Caught || !agent.isOnNavMesh) return;
        if (Vector3.Distance(transform.position, position) > radius) return;

        if (CurrentState == State.Patrol || CurrentState == State.Suspicious)
        {
            Investigate(position);
        }
        else if (CurrentState == State.Search)
        {
            lastKnownPosition = position;
            agent.SetDestination(position);
        }
    }

    // ───────────── 隠れているプレイヤー ─────────────

    /// <summary>
    /// 隠れるところを見られていたら、ロッカーを開けて引きずり出す（ゲームオーバー）。
    /// 見られていなければ見つからないが、近くを通るとたまにロッカーを叩く。
    /// true を返したフレームは、ほかの行動をしない。
    /// </summary>
    bool UpdateHiddenPlayer(float dt)
    {
        bool hidden = playerHiding != null && playerHiding.IsHidden;
        if (hidden && !wasPlayerHidden)
        {
            // 隠れた瞬間に、追いかけていて見えていたか
            sawPlayerHide = CurrentState == State.Spotted || (CurrentState == State.Chase && lostSightTimer < 0.5f);
            bangDecided = false;
        }
        wasPlayerHidden = hidden;
        if (!hidden) return false;

        var spot = playerHiding.CurrentSpot;
        if (sawPlayerHide)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(spot.ExitPoint.position);
            if (Vector3.Distance(transform.position, spot.ExitPoint.position) <= catchDistance + 0.3f)
            {
                FaceTowards(spot.transform.position, dt);
                SetState(State.Caught);
            }
            return true;
        }

        bool searching = CurrentState == State.Search || CurrentState == State.Suspicious;
        if (searching && !bangDecided && Vector3.Distance(transform.position, spot.transform.position) <= bangDistance)
        {
            // 近くを通るたびではなく、1回隠れるごとに1度だけ判定する
            bangDecided = true;
            if (Random.value < bangChance) StartCoroutine(BangLocker(spot));
        }
        return false;
    }

    IEnumerator BangLocker(HidingSpot spot)
    {
        isBanging = true;
        agent.isStopped = true;

        float timer = 0f;
        while (timer < 0.8f)
        {
            FaceTowards(spot.transform.position, Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
        spot.Bang();
        yield return new WaitForSeconds(1.5f);

        agent.isStopped = false;
        isBanging = false;
    }

    // ───────────── 視線 ─────────────

    /// <summary>相沢の視界にプレイヤーが入っていて、間に壁がない</summary>
    bool CanSeePlayer()
    {
        if (playerHiding != null && playerHiding.IsHidden) return false;
        if (distanceToPlayer > sightDistance) return false;

        Vector3 toPlayer = playerEyes.position - eyes.position;
        if (Vector3.Angle(transform.forward, new Vector3(toPlayer.x, 0f, toPlayer.z)) > sightAngle * 0.5f) return false;

        if (Physics.Linecast(eyes.position, playerEyes.position, out var hit, sightMask, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(player.transform);
        return true;
    }

    /// <summary>プレイヤーが相沢の方を見ている（画面の中央付近に映っている）</summary>
    bool IsPlayerLookingAtMe()
    {
        Vector3 toMe = eyes.position - playerEyes.position;
        return Vector3.Angle(playerEyes.forward, toMe) <= playerLookAngle;
    }

    bool HasArrived() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f;

    void FaceTowards(Vector3 target, float dt)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * dt);
    }

    // ───────────── デバッグ表示 ─────────────

    void OnGUI()
    {
        if (!showDebug) return;
        debugStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
        GUI.Label(new Rect(10f, 80f, 500f, 30f), $"相沢: {StateLabel(CurrentState)}　距離 {distanceToPlayer:0.0}m", debugStyle);
    }

    static string StateLabel(State state) => state switch
    {
        State.Patrol => "徘徊",
        State.Suspicious => "警戒（怪しむ）",
        State.Spotted => "発見",
        State.Chase => "追跡",
        State.Search => "探索",
        _ => "捕まえた",
    };

    void OnDrawGizmosSelected()
    {
        // 見つかる距離と、目が合わなくても見つかる距離
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, sightDistance);
        Gizmos.color = new Color(1f, 0f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, closeDetectDistance);
    }
}
