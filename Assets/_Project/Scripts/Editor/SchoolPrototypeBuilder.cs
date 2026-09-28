using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using B = PrototypeSceneBuilder;

/// <summary>
/// メニュー「放課後 > 校舎プロトタイプ（オープニング）を作成」で、
/// 3階の 2年3組から始まるオープニングのシーンを自動で作る。
///
/// 校舎は4階建てで東西に長い。南側に教室（窓から校庭が見える）、北側に廊下。
/// 西と東の両端に折り返し階段がある。
///   西階段 | 部屋1 | 部屋2 | 部屋3 | 部屋4 | 部屋5 | 部屋6 | 東階段
/// 3階の部屋はすべて入れる（2年3組以外は鍵なし。逃げ込める）。
/// ほかの階の部屋・1階の出口・体育館の中は、まだ作っていない（次の段階）。
/// </summary>
public static class SchoolPrototypeBuilder
{
    const string ScenePath = "Assets/Scenes/School_Prototype.unity";
    const string ClassroomKeyId = "key_2-3";
    const string WalkwayKeyId = "key_walkway";
    const string GateKeyId = "key_gate";
    const string NeverOpens = "locked_forever";

    const int FloorCount = 4;
    const float FloorHeight = 3.5f;
    const float WallHeight = 3.3f;
    const float BuildingLength = 60f;
    const float StairWidth = 3f;
    const float RoomWidth = 9f;
    const float CorridorZ = 1.5f;       // 廊下の中央（廊下は z 0〜3）
    const float WindowZ = -8.1f;        // 南の壁（窓側）
    const float BuildingTop = FloorHeight * (FloorCount - 1) + WallHeight;

    // 折り返し階段：1階分を 11段 × 2 で上る
    const int StepsPerFlight = 11;
    const float StepRise = FloorHeight / 2f / StepsPerFlight;
    const float StepRun = 0.28f;
    const float FlightStartZ = -0.5f;
    const float FlightEndZ = FlightStartZ - StepsPerFlight * StepRun;
    const float MidLandingEndZ = -5.1f;

    const int MyFloor = 2;              // 3階
    const int MyRoom = 2;               // 2年3組

    static readonly string[][] RoomNames =
    {
        new[] { "職員室", "校長室", "昇降口", "保健室", "放送室", "事務室" },
        new[] { "3年1組", "3年2組", "3年3組", "3年4組", "理科室", "図書室" },
        new[] { "2年1組", "2年2組", "2年3組", "2年4組", "図工室", "視聴覚室" },
        new[] { "1年1組", "1年2組", "1年3組", "1年4組", "音楽室", "音楽準備室" },
    };

    const string Letter01 =
        "今日も俺の机だけ離れていた。\n誰が動かしたのかは知ってる。\nでも、誰も元に戻さなかった。";

    const string Letter04 =
        "先生に言おうと思った。\n職員室の前まで来て、やめた。\n先生は、あいつらと笑って話していたから。";

    const string Letter08 =
        "先生が来た瞬間だけ、みんな普通になる。\n先生がいなくなると、\n俺はまた、いないことになる。";

    const string Letter09 =
        "グループ分けで、最後まで名前を呼ばれなかった。\n先生が「じゃあ相沢はここ」と決めた。\nそのときのみんなの顔を、今でも覚えている。";

    const string FriendChatLog =
        "［2年3組 男子］\n\n" +
        "佐藤：明日あいつの机どうする？\n" +
        "山本：また離しとこうぜ\n" +
        "佐藤：高橋もそれでいいよな\n" +
        "高橋：まあ、いいんじゃね\n\n" +
        "――相沢 悠 はこのグループに参加していません\n\n" +
        "――今日 16:46――\n" +
        "山本 がグループを退出しました\n" +
        "佐藤：おい山本？どこ行った\n" +
        "佐藤：机が\n" +
        "佐藤 がグループを退出しました";

    // ───── 友人たち（佐藤・山本）がすでに「仕返し」されたことを匂わせる痕跡 ─────
    // 高橋たちが相沢にしたことが、そのまま二人に返ってきている。直接は見せない。

    const string PhoneMonologue = "（……退出？　さっきまで、ここにいたのに）";
    const string BlackboardText = "日直の欄に「佐藤」「山本」と書かれている。\n……二人の名前の上に、線が引かれている。";
    const string IsolatedDeskText = "机の横に「佐藤」と書かれた名札が貼られている。\n中は空っぽだ。";
    const string NoteMonologue = "（……あの日、相沢の机に置かれていた紙と同じだ）";
    const string ShoeMonologue = "（山本の上履き……。俺たちが、相沢の上履きを隠したときみたいに）";
    const string ClassPhotoText = "スクリーンに、クラス写真が映っている。\n……佐藤と山本の顔だけ、黒く塗りつぶされている。\n写真の端に一人だけ、離れて立っている生徒がいる。";
    const string ClayText = "作りかけの粘土の像が並んでいる。どれも顔の部分だけ、削り取られている。\n……端の2つだけ、まだ新しい。佐藤と山本に、似ている。";

    static float FloorY(int floor) => floor * FloorHeight;
    static float RoomX0(int room) => StairWidth + RoomWidth * room;
    static float FrontDoorX(int room) => RoomX0(room) + 1.5f;
    static float BackDoorX(int room) => RoomX0(room) + 7.5f;
    static float MyFloorY => FloorY(MyFloor);

    /// <summary>中に入れる部屋：3階はすべて、1階は職員室だけ</summary>
    static bool IsRoomOpen(int floor, int room) => floor == MyFloor || (floor == 0 && room == StaffRoom);
    const int StaffRoom = 0;            // 1階 職員室

    [MenuItem("放課後/校舎プロトタイプ（オープニング）を作成")]
    static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("確認", $"{ScenePath} はすでにあります。作り直しますか？", "作り直す", "やめる"))
            return;

        int firstPersonLayer = B.EnsureLayer(FirstPersonLayer.Name);
        B.EnsureNavMeshAgentSize();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var defaultCamera = Object.FindAnyObjectByType<Camera>();
        if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

        // 西から差し込む、低い夕日
        var sun = Object.FindAnyObjectByType<Light>();
        sun.name = "Sun";
        sun.color = new Color(1f, 0.55f, 0.3f);
        sun.intensity = 1.4f;
        sun.transform.rotation = Quaternion.Euler(8f, 60f, 0f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.006f;
        RenderSettings.fogColor = new Color(0.85f, 0.55f, 0.4f);

        var environment = new GameObject("Environment").transform;
        BuildShell(environment);
        BuildStairwell(0f, "Stairs_West", environment);
        BuildStairwell(BuildingLength - StairWidth, "Stairs_East", environment);
        for (int floor = 0; floor < FloorCount; floor++) BuildFloor(floor, environment);
        var ceilingLights = BuildMyClassroom(environment);
        BuildOtherRoomsOnMyFloor(environment);
        BuildCorridorDecor(environment);
        BuildStaffRoom(environment);
        var aizawaAppearPoint = BuildWalkwayAndGym(environment);
        var gate = BuildYard(environment);

        var surface = environment.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        environment.gameObject.AddComponent<RuntimeNavMeshBaker>();

        // プレイヤー：2年3組の中、友人のほうを向いて立っている（窓は背中側）
        var player = B.BuildPlayer(new Vector3(26.7f, MyFloorY, -2.9f), firstPersonLayer);
        var playerController = player.GetComponent<PlayerController>();
        var playerCamera = player.GetComponentInChildren<Camera>();

        var gameOver = new GameObject("GameOver").AddComponent<GameOver>();
        B.SetReference(gameOver, "player", playerController);
        B.SetReference(gameOver, "playerCamera", playerCamera);

        // 追いかけてくる相沢：オープニングの後、東側の廊下に現れて、階段を使って校舎中を歩き回る
        Vector3[] patrol =
        {
            new(48f, MyFloorY, CorridorZ),          // 3階 東側の廊下
            new(42.8f, MyFloorY, -6.5f),            // 3階 図工室の中（作業台の窓側）
            new(8f, MyFloorY, CorridorZ),           // 3階 西側の廊下
            new(30f, FloorY(1), CorridorZ),         // 2階（東階段で下りる）
            new(6f, FloorY(1), CorridorZ),          // 2階 西側
            new(30f, FloorY(0), CorridorZ),         // 1階 廊下（西階段で下りる）
            new(30f, FloorY(3), CorridorZ),         // 4階
            new(52f, MyFloorY, CorridorZ),          // 3階に戻る
        };
        var chaser = B.BuildAizawa(patrol[0], 270f, patrol, playerController, playerCamera.transform, gameOver);

        // 演出用：消える友人と、校庭に立つ相沢
        var friends = new[]
        {
            B.CreateFigure("Friend_Sato", new Vector3(26.1f, MyFloorY, -1.2f), 160f, false),
            B.CreateFigure("Friend_Yamamoto", new Vector3(27.3f, MyFloorY, -1.0f), 200f, false),
        };
        var yardAizawa = B.CreateFigure("YardAizawa", new Vector3(30f, 0f, -35f), 0f, true);

        var markers = new GameObject("OpeningMarkers").transform;
        var windowView = CreateMarker("WindowView", new Vector3(26f, 5f, -25f), markers);
        var bangPoint = CreateMarker("BangPoint", new Vector3(6f, MyFloorY + 1f, CorridorZ), markers);
        var stepsFrom = CreateMarker("FootstepsFrom", new Vector3(59f, MyFloorY - 1.5f, -2f), markers);
        var stepsTo = CreateMarker("FootstepsTo", new Vector3(50f, MyFloorY + 0.1f, CorridorZ), markers);

        var opening = new GameObject("Opening").AddComponent<OpeningSequence>();
        B.SetReference(opening, "playerCamera", playerCamera);
        B.SetReference(opening, "windowView", windowView);
        B.SetReference(opening, "yardAizawa", yardAizawa);
        B.SetReference(opening, "chaser", chaser);
        B.SetReference(opening, "bangPoint", bangPoint);
        B.SetReference(opening, "footstepsFrom", stepsFrom);
        B.SetReference(opening, "footstepsTo", stepsTo);
        SetArray(opening, "friends", friends);
        var lights = new Light[ceilingLights.Length + 1];
        lights[0] = sun;
        ceilingLights.CopyTo(lights, 1);
        SetArray(opening, "flickerLights", lights);

        // 校門：鍵を開けるのに3秒かかる。外に出るとエンディング
        var inventory = player.GetComponent<Inventory>();
        gate.isStatic = false;
        gate.AddComponent<GateLock>().Setup(gate.transform, GateKeyId);

        var ending = new GameObject("Ending").AddComponent<EndingSequence>();
        var letterOnDesk = B.CreateBox("EndingLetter", new Vector3(RoomX0(MyRoom) + 6.6f, MyFloorY + 0.737f, -3.6f),
            new Vector3(0.15f, 0.002f, 0.21f), B.GetOrCreateMaterial("Paper", new Color(0.95f, 0.94f, 0.9f)), markers);
        letterOnDesk.isStatic = false;
        var wakeUpPoint = CreateMarker("WakeUpPoint", player.transform.position, markers);
        B.SetReference(ending, "player", playerController);
        B.SetReference(ending, "playerCamera", playerCamera);
        B.SetReference(ending, "inventory", inventory);
        B.SetReference(ending, "wakeUpPoint", wakeUpPoint);
        B.SetReference(ending, "chaser", chaser);
        B.SetReference(ending, "windowView", windowView);
        B.SetReference(ending, "letterOnDesk", letterOnDesk);
        SetArray(ending, "friends", friends);

        CreateZone<EscapeTrigger>("EscapeZone", new Vector3(30f, 1.5f, -73f), new Vector3(8f, 3f, 4f), playerController).Setup(ending);

        // クライマックス：校門の鍵を持って体育館から校庭に出ると、相沢が体育館から現れる
        CreateZone<ClimaxTrigger>("ClimaxZone", new Vector3(76f, 1.5f, -17f), new Vector3(8f, 3f, 4f), playerController)
            .Setup(inventory, chaser.GetComponent<AizawaAI>(), aizawaAppearPoint);

        Selection.activeGameObject = player;
        EditorSceneManager.SaveScene(scene, ScenePath);
        B.AddSceneToBuildSettings(ScenePath);
        Debug.Log($"校舎プロトタイプを作成しました: {ScenePath}　再生ボタンを押すとオープニングが始まります。");
    }

    // ───────────── 校舎の外側 ─────────────

    static void BuildShell(Transform environment)
    {
        var root = Group("Shell", environment);
        var wallMat = WallMaterial();

        B.CreateBox("Roof", new Vector3(BuildingLength / 2f, BuildingTop + 0.2f, -2.5f), new Vector3(BuildingLength + 0.4f, 0.4f, 11.4f), wallMat, root);
        B.CreateBox("Wall_North", new Vector3(BuildingLength / 2f, BuildingTop / 2f, 3.1f), new Vector3(BuildingLength + 0.4f, BuildingTop, 0.2f), wallMat, root);
        B.CreateBox("Wall_WestEnd", new Vector3(-0.1f, BuildingTop / 2f, -2.5f), new Vector3(0.2f, BuildingTop, 11.4f), wallMat, root);
        // 東の端の壁：1階の廊下の突き当たりに、渡り廊下への扉の穴（z 1〜2、高さ2m）がある
        float eastX = BuildingLength + 0.1f;
        B.CreateBox("Wall_EastEnd_South", new Vector3(eastX, BuildingTop / 2f, -3.6f), new Vector3(0.2f, BuildingTop, 9.2f), wallMat, root);
        B.CreateBox("Wall_EastEnd_North", new Vector3(eastX, BuildingTop / 2f, 2.6f), new Vector3(0.2f, BuildingTop, 1.2f), wallMat, root);
        B.CreateBox("Wall_EastEnd_DoorTop", new Vector3(eastX, (2f + BuildingTop) / 2f, 1.5f), new Vector3(0.2f, BuildingTop - 2f, 1f), wallMat, root);
    }

    // ───────────── 階段（西・東） ─────────────

    /// <summary>
    /// 折り返し階段。各階の踊り場から、片側の列を上って中間の踊り場で折り返し、もう片側の列で次の階に着く。
    /// </summary>
    static void BuildStairwell(float x0, string name, Transform environment)
    {
        var root = Group(name, environment);
        var wallMat = WallMaterial();
        var floorMat = FloorMaterial();
        var stepMat = B.GetOrCreateMaterial("Step", new Color(0.6f, 0.58f, 0.55f));
        var railMat = B.GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f));

        float xMid = x0 + StairWidth / 2f;
        float upX = x0 + 0.75f;      // 上りの列
        float downX = x0 + 2.25f;    // 折り返して次の階へ着く列
        float sideX = x0 < 1f ? StairWidth : x0;

        // 窓側の奥はふさぐ・教室との仕切り・上りと下りの間の壁（全部の階をつらぬく）
        B.CreateBox("BackBlock", new Vector3(xMid, BuildingTop / 2f, (MidLandingEndZ - 8f) / 2f), new Vector3(StairWidth, BuildingTop, MidLandingEndZ + 8f), wallMat, root);
        B.CreateBox("SideWall", new Vector3(sideX, BuildingTop / 2f, -4f), new Vector3(0.2f, BuildingTop, 8f), wallMat, root);
        B.CreateBox("Divider", new Vector3(xMid, BuildingTop / 2f, (FlightStartZ + FlightEndZ) / 2f), new Vector3(0.1f, BuildingTop, FlightStartZ - FlightEndZ), wallMat, root);

        for (int floor = 0; floor < FloorCount; floor++)
        {
            float y = FloorY(floor);

            // 各階の踊り場（廊下とつながる）。1階は階段の下まで床がある
            if (floor == 0)
                B.CreateBox("Landing_1F", new Vector3(xMid, -0.1f, (MidLandingEndZ + 3f) / 2f), new Vector3(StairWidth, 0.2f, 3f - MidLandingEndZ), floorMat, root);
            else
                B.CreateBox($"Landing_{floor + 1}F", new Vector3(xMid, y - 0.1f, (FlightStartZ + 3f) / 2f), new Vector3(StairWidth, 0.2f, 3f - FlightStartZ), floorMat, root);

            if (floor == FloorCount - 1)
            {
                // 最上階：上りの列の先は吹き抜けなので、手すりと見えない壁で落ちないようにする
                B.CreateBox("TopRail", new Vector3(upX, y + 0.5f, FlightStartZ - 0.03f), new Vector3(1.45f, 1f, 0.05f), railMat, root);
                var guard = new GameObject("TopGuard") { layer = 2 };
                guard.transform.SetParent(root, false);
                guard.transform.position = new Vector3(upX, y + WallHeight / 2f, FlightStartZ - 0.1f);
                guard.AddComponent<BoxCollider>().size = new Vector3(1.5f, WallHeight, 0.2f);
                continue;
            }

            // 上り：踊り場から窓側へ
            for (int i = 0; i < StepsPerFlight; i++)
            {
                float top = y + (i + 1) * StepRise;
                float z = FlightStartZ - (i + 0.5f) * StepRun;
                CreateStep($"Up_{floor + 1}F_{i}", upX, top, z, stepMat, root);
            }

            // 中間の踊り場
            B.CreateBox($"MidLanding_{floor + 1}F", new Vector3(xMid, y + FloorHeight / 2f - 0.1f, (FlightEndZ + MidLandingEndZ) / 2f),
                new Vector3(StairWidth, 0.2f, FlightEndZ - MidLandingEndZ), floorMat, root);

            // 折り返して、廊下側へ上って次の階に着く
            for (int i = 0; i < StepsPerFlight; i++)
            {
                float top = y + FloorHeight / 2f + (i + 1) * StepRise;
                float z = FlightEndZ + (i + 0.5f) * StepRun;
                CreateStep($"Turn_{floor + 1}F_{i}", downX, top, z, stepMat, root);
            }
        }
    }

    static void CreateStep(string name, float x, float top, float z, Material material, Transform parent)
    {
        float height = StepRise + 0.2f;
        B.CreateBox(name, new Vector3(x, top - height / 2f, z), new Vector3(1.45f, height, StepRun), material, parent);
    }

    // ───────────── 各階の床・壁・扉 ─────────────

    static void BuildFloor(int floor, Transform environment)
    {
        var root = Group($"{floor + 1}F", environment);
        var wallMat = WallMaterial();
        var glassMat = B.GetOrCreateMaterial("DarkGlass", new Color(0.15f, 0.2f, 0.25f));
        var frameMat = B.GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f));
        float y = FloorY(floor);
        float wallY = y + WallHeight / 2f;
        float roomsFrom = StairWidth;
        float roomsTo = BuildingLength - StairWidth;
        string[] names = RoomNames[floor];

        B.CreateBox($"Floor_{floor + 1}F", new Vector3(BuildingLength / 2f, y - 0.1f, -2.5f), new Vector3(roomsTo - roomsFrom, 0.2f, 11f), FloorMaterial(), root);

        // 部屋どうしの仕切り
        for (int i = 1; i < names.Length; i++)
            B.CreateBox($"Partition_{i}", new Vector3(RoomX0(i), wallY, -4f), new Vector3(0.2f, WallHeight, 8f), wallMat, root);

        // 廊下側の壁：入れる部屋の扉の部分だけ開いている
        var doorXs = new List<float>();
        for (int i = 0; i < names.Length; i++)
        {
            if (!IsRoomOpen(floor, i)) continue;
            doorXs.Add(FrontDoorX(i));
            doorXs.Add(BackDoorX(i));
        }
        float from = roomsFrom;
        for (int d = 0; d < doorXs.Count; d++)
        {
            float x = doorXs[d];
            WallX($"CorridorWall_{d}", from, x - 0.5f, 0f, y, y + WallHeight, wallMat, root);
            WallX($"DoorTop_{d}", x - 0.5f, x + 0.5f, 0f, y + 2f, y + WallHeight, wallMat, root);
            from = x + 0.5f;
        }
        WallX("CorridorWall_End", from, roomsTo, 0f, y, y + WallHeight, wallMat, root);

        for (int i = 0; i < names.Length; i++)
        {
            CreateRoomPlate(FrontDoorX(i), y, names[i], root);

            if (!IsRoomOpen(floor, i))
            {
                // まだ中を作っていない部屋：窓の形だけで、扉は開かない
                WallX($"WindowWall_{i}", RoomX0(i), RoomX0(i + 1), WindowZ, y, y + WallHeight, wallMat, root);
                B.CreateBox($"FakeWindow_{i}", new Vector3(RoomX0(i) + RoomWidth / 2f, y + 1.9f, WindowZ - 0.12f), new Vector3(8f, 1.8f, 0.02f), glassMat, root);

                string message = names[i] == "昇降口" ? "昇降口のシャッターが下りている。外には出られない。" : "鍵がかかっている。";
                CreateFakeDoor($"Door_{i}_Front", FrontDoorX(i), y, $"{names[i]}の扉", message, root);
                if (names[i] != "昇降口") CreateFakeDoor($"Door_{i}_Back", BackDoorX(i), y, $"{names[i]}の扉", message, root);
                continue;
            }

            BuildWindows(i, y, wallMat, frameMat, root);

            // 掃除用具ロッカー：教室の後ろ（東の壁ぎわ）、廊下側。扉は教室の中を向く
            B.CreateCleaningLocker($"Locker_{names[i]}", new Vector3(RoomX0(i + 1) - 0.4f, y, -1.4f), -90f, root);

            if (floor == MyFloor && i == MyRoom)
            {
                // 2年3組：前の扉は鍵がかかっている（鍵は相沢の机の中）、後ろの扉は開かない
                CreateSlidingDoor($"Door_{i}_Front", FrontDoorX(i), y, $"{names[i]}の前の扉", ClassroomKeyId,
                    "鍵がかかっている。……外から？\nどこかに鍵はないか。", root);
                CreateSlidingDoor($"Door_{i}_Back", BackDoorX(i), y, $"{names[i]}の後ろの扉", NeverOpens,
                    "こっちの扉も開かない。", root);
            }
            else
            {
                // ほかの部屋は鍵がかかっていない。相沢から逃げ込める
                CreateSlidingDoor($"Door_{i}_Front", FrontDoorX(i), y, $"{names[i]}の前の扉", "", "", root);
                CreateSlidingDoor($"Door_{i}_Back", BackDoorX(i), y, $"{names[i]}の後ろの扉", "", "", root);
            }
        }
    }

    /// <summary>腰壁・上の壁・窓枠。落ちないように見えない壁を置く</summary>
    static void BuildWindows(int room, float y, Material wallMat, Material frameMat, Transform parent)
    {
        float x0 = RoomX0(room);
        float x1 = RoomX0(room + 1);
        float bottom = y + 1f;
        float top = y + 2.8f;

        WallX($"Window_{room}_Lower", x0, x1, WindowZ, y, bottom, wallMat, parent);
        WallX($"Window_{room}_Upper", x0, x1, WindowZ, top, y + WallHeight, wallMat, parent);
        for (int i = 1; i < 5; i++)
        {
            float x = x0 + RoomWidth / 5f * i;
            B.CreateBox($"WindowFrame_{room}_{i}", new Vector3(x, (bottom + top) / 2f, WindowZ), new Vector3(0.12f, top - bottom, 0.2f), frameMat, parent);
        }

        // Ignore Raycast レイヤーにして、窓の外を「見た」判定のじゃまをしないようにする
        var guard = new GameObject($"WindowGuard_{room}") { layer = 2 };
        guard.transform.SetParent(parent, false);
        guard.transform.position = new Vector3((x0 + x1) / 2f, (bottom + top) / 2f, WindowZ);
        guard.AddComponent<BoxCollider>().size = new Vector3(RoomWidth, top - bottom, 0.2f);
    }

    // ───────────── 2年3組の中 ─────────────

    static Light[] BuildMyClassroom(Transform environment)
    {
        var root = Group("Classroom_2-3", environment);
        var goldMat = B.GetOrCreateMaterial("Gold", new Color(0.85f, 0.7f, 0.3f));
        var paperMat = B.GetOrCreateMaterial("Paper", new Color(0.95f, 0.94f, 0.9f));
        var phoneMat = B.GetOrCreateMaterial("Phone", new Color(0.08f, 0.08f, 0.1f));
        var fixtureMat = B.GetOrCreateMaterial("LightFixture", new Color(1f, 1f, 0.95f));
        float x0 = RoomX0(MyRoom);
        float y = MyFloorY;

        var board = CreateClassroomFront(x0, y, root);
        board.GetComponent<InfoSign>().Setup("黒板", BlackboardText);

        // 黒板の右端の日直の欄：縦書きの二人の名前（チョークの線）と、その上に引かれた横線
        var chalkMat = B.GetOrCreateMaterial("Chalk", new Color(0.92f, 0.92f, 0.88f));
        float boardFace = x0 + 0.16f;
        B.CreateBox("Chalk_Label", new Vector3(boardFace, y + 1.88f, -2.35f), new Vector3(0.01f, 0.012f, 0.18f), chalkMat, root);
        B.CreateBox("Chalk_Name_Sato", new Vector3(boardFace, y + 1.55f, -2.3f), new Vector3(0.01f, 0.4f, 0.035f), chalkMat, root);
        B.CreateBox("Chalk_Name_Yamamoto", new Vector3(boardFace, y + 1.5f, -2.45f), new Vector3(0.01f, 0.5f, 0.035f), chalkMat, root);
        var strike = B.CreateBox("Chalk_Strike", new Vector3(boardFace, y + 1.6f, -2.38f), new Vector3(0.01f, 0.018f, 0.34f), chalkMat, root);
        strike.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

        // 机（3列 × 3行）。どの机も中を調べられる
        string[] deskContents =
        {
            "教科書とノートが入っている。",
            "佐藤の教科書が入っている。",   // スマートフォンが置いてある佐藤の机
            "丸めたプリントが押し込まれている。",
            "何も入っていない。",
            "お菓子の空き袋が入っている。",
            "体操服の袋が入っている。",
            "何も入っていない。",
            "辞書が一冊だけ入っている。",
            "落書きだらけのノートが入っている。",
        };
        CreateDeskGrid(x0, y, deskContents, root);

        var phone = B.CreateBox("FriendPhone", new Vector3(x0 + 4.8f, y + 0.74f, -2f), new Vector3(0.07f, 0.008f, 0.15f), phoneMat, root);
        phone.isStatic = false;
        phone.AddComponent<ItemPickup>().Setup("phone_sato", "佐藤のスマートフォン",
            "さっきまで話していた佐藤のものだ。画面がついたままになっている。", FriendChatLog, PhoneMonologue);

        // 相沢の机：窓際の一番後ろ、ほかの机から離れている
        var aizawaDeskPosition = new Vector3(x0 + 8.2f, y, -7.2f);
        var aizawaDesk = B.CreateDesk("Desk_Aizawa", aizawaDeskPosition, root);

        var letter = B.CreateBox("Letter_01", aizawaDeskPosition + new Vector3(0f, 0.737f, 0f), new Vector3(0.15f, 0.002f, 0.21f), paperMat, root);
        letter.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
        letter.isStatic = false;
        letter.AddComponent<ItemPickup>().Setup("letter_01", "手紙", "相沢の机の上に置かれていた紙。", Letter01);

        var key = new GameObject("Key_2-3");
        key.transform.SetParent(root, false);
        key.transform.position = aizawaDeskPosition + new Vector3(0f, 0.64f, 0f);
        B.CreateBox("Shaft", new Vector3(0f, 0f, 0.02f), new Vector3(0.012f, 0.004f, 0.06f), goldMat, key.transform, true);
        B.CreateBox("Bow", new Vector3(0f, 0f, -0.025f), new Vector3(0.035f, 0.005f, 0.03f), goldMat, key.transform, true);
        B.CreateBox("Teeth", new Vector3(0.01f, 0f, 0.045f), new Vector3(0.012f, 0.004f, 0.012f), goldMat, key.transform, true);
        var keyPickup = key.AddComponent<ItemPickup>();
        keyPickup.Setup(ClassroomKeyId, "2年3組の鍵", "教室の扉の鍵。なぜ相沢の机の中に？");
        aizawaDesk.AddComponent<ItemContainer>().Setup("机", keyPickup, "もう何も入っていない。");
        key.SetActive(false);

        // 蛍光灯（オープニングで一瞬消える）
        var lights = new Light[2];
        for (int i = 0; i < lights.Length; i++)
        {
            var position = new Vector3(x0 + 3f + 3f * i, y + WallHeight - 0.05f, -4f);
            B.CreateBox($"LightFixture_{i}", position, new Vector3(1.2f, 0.05f, 0.2f), fixtureMat, root);
            var lightObject = new GameObject($"CeilingLight_{i}");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.position = position + Vector3.down * 0.2f;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 2f;
            light.range = 7f;
            lights[i] = light;
        }
        return lights;
    }

    // ───────────── 3階のほかの部屋（逃げ込める） ─────────────

    static void BuildOtherRoomsOnMyFloor(Transform environment)
    {
        var root = Group("OtherRooms_3F", environment);
        var woodMat = B.GetOrCreateMaterial("Wood", new Color(0.7f, 0.55f, 0.38f));
        var frameMat = B.GetOrCreateMaterial("Phone", new Color(0.08f, 0.08f, 0.1f));
        var chairMat = B.GetOrCreateMaterial("Chair", new Color(0.3f, 0.32f, 0.4f));
        var paperMat = B.GetOrCreateMaterial("Paper", new Color(0.95f, 0.94f, 0.9f));
        var clayMat = B.GetOrCreateMaterial("Clay", new Color(0.62f, 0.58f, 0.52f));
        var newClayMat = B.GetOrCreateMaterial("NewClay", new Color(0.45f, 0.38f, 0.32f));
        float y = MyFloorY;

        string[] classroomDesks =
        {
            "教科書が入っている。", "何も入っていない。", "プリントの束が入っている。",
            "何も入っていない。", "ノートが入っている。", "何も入っていない。",
            "筆箱が入っている。", "何も入っていない。", "教科書が入っている。",
        };

        for (int i = 0; i < RoomNames[MyFloor].Length; i++)
        {
            if (i == MyRoom) continue;
            float x0 = RoomX0(i);
            var room = Group(RoomNames[MyFloor][i], root);

            switch (RoomNames[MyFloor][i])
            {
                case "2年2組":
                    // 机がすべて壁ぎわに寄せられ、真ん中に佐藤の机だけが置かれている（相沢の机を離したのと同じ）
                    CreateClassroomFront(x0, y, room);
                    for (int d = 0; d < 6; d++)
                    {
                        var pushedNorth = B.CreateDesk($"PushedDesk_N{d}", new Vector3(x0 + 2.4f + 0.9f * d, y, -0.8f), room);
                        pushedNorth.transform.rotation = Quaternion.Euler(0f, (d * 37) % 20 - 10f, 0f);
                        var pushedSouth = B.CreateDesk($"PushedDesk_S{d}", new Vector3(x0 + 2.4f + 0.9f * d, y, -7.3f), room);
                        pushedSouth.transform.rotation = Quaternion.Euler(0f, (d * 53) % 20 - 10f, 0f);
                    }
                    var satoDeskPosition = new Vector3(x0 + 4.8f, y, -4f);
                    var satoDesk = B.CreateDesk("Desk_Sato", satoDeskPosition, room);
                    satoDesk.AddComponent<ItemContainer>().Setup("机", null, IsolatedDeskText);
                    var note = B.CreateBox("Note_ForSato", satoDeskPosition + new Vector3(0f, 0.737f, 0f), new Vector3(0.15f, 0.002f, 0.21f), paperMat, room);
                    note.transform.rotation = Quaternion.Euler(0f, -10f, 0f);
                    note.isStatic = false;
                    note.AddComponent<ItemPickup>().Setup("note_sato", "紙切れ", "佐藤の机の上に置かれていた。", "明日から来なくていい", NoteMonologue);
                    break;

                case "図工室":
                    // 大きな作業台が3つ。真ん中の台に、顔を削られた粘土の像と、新しい2つの像
                    for (int t = 0; t < 3; t++)
                    {
                        var table = B.CreateBox($"WorkTable_{t}", new Vector3(x0 + 2.5f + 2.5f * t, y + 0.4f, -4f), new Vector3(1.8f, 0.8f, 1f), woodMat, room);
                        if (t != 1) continue;

                        table.AddComponent<InfoSign>().Setup("作業台", ClayText, "調べる");
                        for (int s = 0; s < 5; s++)
                        {
                            bool isNew = s >= 3;
                            CreateClayFigure($"Clay_{s}", new Vector3(x0 + 4.3f + 0.35f * s, y + 0.8f, -4f), isNew ? newClayMat : clayMat, isNew, room);
                        }
                    }
                    break;

                case "視聴覚室":
                    // 前のスクリーンにクラス写真が映っている。椅子が並んでいる
                    B.CreateBox("ScreenFrame", new Vector3(x0 + 0.13f, y + 1.6f, -4f), new Vector3(0.05f, 1.9f, 3.3f), frameMat, room);
                    var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    screen.name = "Screen_ClassPhoto";
                    screen.transform.SetParent(room, false);
                    screen.transform.SetPositionAndRotation(new Vector3(x0 + 0.16f, y + 1.6f, -4f), Quaternion.Euler(0f, -90f, 0f));
                    screen.transform.localScale = new Vector3(3.2f, 1.8f, 1f);
                    screen.GetComponent<Renderer>().sharedMaterial = GetOrCreateScreenMaterial();
                    screen.AddComponent<InfoSign>().Setup("スクリーン", ClassPhotoText);
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                            B.CreateBox($"Chair_{row}_{col}", new Vector3(x0 + 3f + 1.4f * row, y + 0.225f, -6.2f + 1.4f * col), new Vector3(0.45f, 0.45f, 0.45f), chairMat, room);
                    break;

                default:
                    CreateClassroomFront(x0, y, room);
                    CreateDeskGrid(x0, y, classroomDesks, room);
                    break;
            }
        }
    }

    /// <summary>
    /// 高さ30cmほどの粘土の像。古い像は顔が削られてのっぺりしている。
    /// 新しい像（isNew）は短い髪があり、佐藤と山本に似ている。
    /// </summary>
    static void CreateClayFigure(string name, Vector3 position, Material material, bool isNew, Transform parent)
    {
        var figure = new GameObject(name).transform;
        figure.SetParent(parent, false);
        figure.position = position;
        figure.rotation = Quaternion.Euler(0f, 90f, 0f);   // 部屋の入口側を向く

        B.CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.09f, 0f), new Vector3(0.1f, 0.09f, 0.08f), material, figure);
        B.CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.22f, 0f), Vector3.one * 0.075f, material, figure);
        if (!isNew)
        {
            // 顔の部分が削り取られている
            var cut = B.GetOrCreateMaterial("ClayCut", new Color(0.4f, 0.37f, 0.33f));
            B.CreatePart(PrimitiveType.Cube, "CutFace", new Vector3(0f, 0.22f, 0.03f), new Vector3(0.05f, 0.05f, 0.02f), cut, figure);
            return;
        }
        var hairMat = B.GetOrCreateMaterial("NewClayHair", new Color(0.3f, 0.25f, 0.2f));
        B.CreatePart(PrimitiveType.Sphere, "Hair", new Vector3(0f, 0.245f, -0.005f), new Vector3(0.08f, 0.055f, 0.08f), hairMat, figure);
    }

    /// <summary>視聴覚室のスクリーンに映るクラス写真（プロジェクターの光のように少し光らせる）</summary>
    static Material GetOrCreateScreenMaterial()
    {
        const string path = "Assets/_Project/Materials/Screen_ClassPhoto.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        var texture = PosterPainter.GetOrCreate(PosterPainter.Design.ClassPhoto);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { mainTexture = texture, color = Color.white };
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_EmissionColor", Color.white * 0.6f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>黒板（西の壁）と教卓。黒板を返す</summary>
    static GameObject CreateClassroomFront(float x0, float y, Transform parent)
    {
        var boardMat = B.GetOrCreateMaterial("Blackboard", new Color(0.12f, 0.25f, 0.18f));
        var woodMat = B.GetOrCreateMaterial("Wood", new Color(0.7f, 0.55f, 0.38f));
        var board = B.CreateBox("Blackboard", new Vector3(x0 + 0.13f, y + 1.4f, -4f), new Vector3(0.05f, 1.2f, 4f), boardMat, parent);
        board.AddComponent<InfoSign>().Setup("黒板", "黒板には何も書かれていない。");
        B.CreateBox("TeacherDesk", new Vector3(x0 + 1.4f, y + 0.5f, -4f), new Vector3(0.6f, 1f, 1.2f), woodMat, parent);
        return board;
    }

    /// <summary>3列 × 3行の机。contents は調べたときのメッセージ</summary>
    static void CreateDeskGrid(float x0, float y, string[] contents, Transform parent)
    {
        float[] columns = { x0 + 3f, x0 + 4.8f, x0 + 6.6f };
        float[] rows = { -2f, -3.6f, -5.2f };
        int index = 0;
        foreach (float z in rows)
        {
            foreach (float x in columns)
            {
                var desk = B.CreateDesk($"Desk_{index + 1}", new Vector3(x, y, z), parent);
                desk.AddComponent<ItemContainer>().Setup("机", null, contents[index % contents.Length]);
                index++;
            }
        }
    }

    // ───────────── 3階の廊下 ─────────────

    static void BuildCorridorDecor(Transform environment)
    {
        var root = Group("Corridor_3F", environment);
        float y = MyFloorY;

        // 廊下のポスター（北の壁）。絵は PosterPainter で描いている
        (PosterPainter.Design design, string title, string text)[] posters =
        {
            (PosterPainter.Design.Bullying, "ポスター", "「いじめ　見て見ぬふりも　いじめです」\n……標語の一部が、黒く塗りつぶされている。"),
            (PosterPainter.Design.Chorus, "ポスター", "「合唱コンクール　10月14日（金）　体育館にて」"),
            (PosterPainter.Design.ArtExhibition, "ポスター", "「図工作品展　開催中　――3階 図工室」"),
            (PosterPainter.Design.Health, "保健だより", "「こころの相談は、いつでも保健室へ」"),
            (PosterPainter.Design.ArtClub, "ポスター", "「美術部　部員募集！」"),
            (PosterPainter.Design.NoRunning, "ポスター", "「廊下は走らない」"),
        };
        float[] posterX = { 9f, 17f, 25f, 34f, 43f, 51f };
        for (int i = 0; i < posters.Length; i++)
        {
            var material = GetOrCreatePosterMaterial(posters[i].design);
            var poster = B.CreateBox($"Poster_{posters[i].design}", new Vector3(posterX[i], y + 1.6f, 2.98f), new Vector3(0.8f, 1.1f, 0.02f), material, root);
            poster.AddComponent<InfoSign>().Setup(posters[i].title, posters[i].text);
        }

        // 廊下に片方だけ落ちている、山本の上履き（相沢の上履きを隠したのと同じ）
        var shoeWhite = B.GetOrCreateMaterial("Shirt", new Color(0.95f, 0.95f, 0.95f));
        var shoeBlue = B.GetOrCreateMaterial("ShoeToe", new Color(0.25f, 0.4f, 0.75f));
        var shoe = new GameObject("Uwabaki_Yamamoto");
        shoe.transform.SetParent(root, false);
        shoe.transform.SetPositionAndRotation(new Vector3(8f, y, 1f), Quaternion.Euler(0f, 35f, 0f));
        B.CreateBox("Sole", new Vector3(0f, 0.01f, 0f), new Vector3(0.1f, 0.02f, 0.26f), shoeWhite, shoe.transform, true);
        B.CreateBox("Upper", new Vector3(0f, 0.045f, -0.02f), new Vector3(0.09f, 0.05f, 0.2f), shoeWhite, shoe.transform, true);
        B.CreateBox("Toe", new Vector3(0f, 0.035f, 0.1f), new Vector3(0.092f, 0.035f, 0.06f), shoeBlue, shoe.transform, true);
        shoe.AddComponent<ItemPickup>().Setup("shoe_yamamoto", "上履き（片方）", "かかとに「山本」と書いてある。", "", ShoeMonologue);

        // 廊下の掃除用具ロッカー（北の壁ぎわ。扉は廊下を向く）
        B.CreateCleaningLocker("Locker_Corridor_West", new Vector3(13f, y, 2.7f), 180f, root);
        B.CreateCleaningLocker("Locker_Corridor_East", new Vector3(38.5f, y, 2.7f), 180f, root);

        // 3階の西階段の入口：机が積まれていて通れない（オープニングの「ガタン」の正体）
        var pile = Group("DeskPile_West", root);
        (Vector3 position, Vector3 rotation)[] pileDesks =
        {
            (new Vector3(3.6f, y, 0.6f), new Vector3(0f, 20f, 0f)),
            (new Vector3(3.8f, y, 2.3f), new Vector3(0f, -15f, 0f)),
            (new Vector3(3.5f, y + 0.76f, 1.4f), new Vector3(0f, 80f, 10f)),
            (new Vector3(3.9f, y + 0.76f, 0.5f), new Vector3(180f, 30f, 0f)),
            (new Vector3(3.7f, y + 1.5f, 1.8f), new Vector3(15f, 60f, 170f)),
        };
        for (int i = 0; i < pileDesks.Length; i++)
        {
            var desk = B.CreateDesk($"PiledDesk_{i}", pileDesks[i].position, pile);
            desk.transform.rotation = Quaternion.Euler(pileDesks[i].rotation);
        }
        var pileBlocker = new GameObject("PileBlocker");
        pileBlocker.transform.SetParent(pile, false);
        pileBlocker.transform.position = new Vector3(3.8f, y + WallHeight / 2f, CorridorZ);
        pileBlocker.AddComponent<BoxCollider>().size = new Vector3(1f, WallHeight, 3f);
        pileBlocker.AddComponent<InfoSign>().Setup("西階段", "机と椅子が積み上げられていて、通れない。\n……さっきまでは、こんなものなかった。", "調べる");
    }

    static Material GetOrCreatePosterMaterial(PosterPainter.Design design)
    {
        string path = $"Assets/_Project/Materials/PosterArt_{design}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
        {
            mainTexture = PosterPainter.GetOrCreate(design),
            color = Color.white,
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // ───────────── 校庭 ─────────────

    /// <summary>校庭・遊具・校門。校門の板を返す</summary>
    static GameObject BuildYard(Transform environment)
    {
        var root = Group("Yard", environment);
        var groundMat = B.GetOrCreateMaterial("Ground", new Color(0.62f, 0.5f, 0.38f));
        var metalMat = B.GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f));
        var redMat = B.GetOrCreateMaterial("PlaygroundRed", new Color(0.7f, 0.2f, 0.15f));
        var yellowMat = B.GetOrCreateMaterial("PlaygroundYellow", new Color(0.85f, 0.7f, 0.2f));
        var tireMat = B.GetOrCreateMaterial("Tire", new Color(0.1f, 0.1f, 0.1f));
        var wallMat = WallMaterial();

        // 地面：校舎の南側（校門の外まで）と、校舎の東側（渡り廊下・体育館のまわり）
        B.CreateBox("Ground", new Vector3(35f, -0.1f, -44.1f), new Vector3(110f, 0.2f, 71.8f), groundMat, root);
        B.CreateBox("Ground_East", new Vector3(75.1f, -0.1f, 0.9f), new Vector3(29.8f, 0.2f, 18.2f), groundMat, root);

        // ブランコ
        B.CreateBox("Swing_PostL", new Vector3(10f, 1.2f, -20f), new Vector3(0.1f, 2.4f, 0.1f), redMat, root);
        B.CreateBox("Swing_PostR", new Vector3(14f, 1.2f, -20f), new Vector3(0.1f, 2.4f, 0.1f), redMat, root);
        B.CreateBox("Swing_Bar", new Vector3(12f, 2.4f, -20f), new Vector3(4.2f, 0.1f, 0.1f), redMat, root);
        foreach (float x in new[] { 11f, 13f })
        {
            B.CreateBox($"Swing_Seat_{x}", new Vector3(x, 0.5f, -20f), new Vector3(0.5f, 0.05f, 0.25f), yellowMat, root);
            B.CreateBox($"Swing_ChainL_{x}", new Vector3(x - 0.22f, 1.45f, -20f), new Vector3(0.02f, 1.9f, 0.02f), metalMat, root);
            B.CreateBox($"Swing_ChainR_{x}", new Vector3(x + 0.22f, 1.45f, -20f), new Vector3(0.02f, 1.9f, 0.02f), metalMat, root);
        }

        // ジャングルジム（1m 間隔の格子）
        var jungleGym = Group("JungleGym", root);
        var origin = new Vector3(16f, 0f, -31f);
        for (int a = 0; a <= 3; a++)
        {
            for (int b = 0; b <= 3; b++)
            {
                B.CreateBox($"Post_{a}_{b}", origin + new Vector3(a, 1.5f, b), new Vector3(0.05f, 3f, 0.05f), yellowMat, jungleGym);
                for (int level = 1; level <= 3; level++)
                {
                    if (a < 3) B.CreateBox($"BarX_{a}_{b}_{level}", origin + new Vector3(a + 0.5f, level, b), new Vector3(1f, 0.05f, 0.05f), yellowMat, jungleGym);
                    if (b < 3) B.CreateBox($"BarZ_{a}_{b}_{level}", origin + new Vector3(a, level, b + 0.5f), new Vector3(0.05f, 0.05f, 1f), yellowMat, jungleGym);
                }
            }
        }

        // 滑り台
        B.CreateBox("Slide_Platform", new Vector3(6f, 1.5f, -30f), new Vector3(1f, 0.1f, 1f), metalMat, root);
        foreach (var leg in new[] { new Vector3(5.55f, 0.75f, -29.55f), new Vector3(6.45f, 0.75f, -29.55f), new Vector3(5.55f, 0.75f, -30.45f), new Vector3(6.45f, 0.75f, -30.45f) })
            B.CreateBox("Slide_Leg", leg, new Vector3(0.06f, 1.5f, 0.06f), metalMat, root);
        var ramp = B.CreateBox("Slide_Ramp", new Vector3(6f, 0.75f, -32f), new Vector3(0.6f, 0.05f, 3.4f), redMat, root);
        ramp.transform.rotation = Quaternion.Euler(-26.6f, 0f, 0f);

        // タイヤ跳び（半分うまったタイヤの列）
        for (int i = 0; i < 6; i++)
        {
            var tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = $"Tire_{i}";
            tire.transform.SetParent(root, false);
            tire.transform.position = new Vector3(42f + 0.8f * i, 0.2f, -18f);
            tire.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            tire.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            tire.GetComponent<Renderer>().sharedMaterial = tireMat;
            tire.isStatic = true;
        }

        // 校門とフェンス
        B.CreateBox("Gate_PillarL", new Vector3(27.5f, 1f, -70f), new Vector3(0.6f, 2f, 0.6f), wallMat, root);
        B.CreateBox("Gate_PillarR", new Vector3(32.5f, 1f, -70f), new Vector3(0.6f, 2f, 0.6f), wallMat, root);
        var gate = B.CreateBox("Gate", new Vector3(30f, 0.9f, -70f), new Vector3(4.4f, 1.6f, 0.08f), metalMat, root);
        B.CreateBox("Fence_SouthW", new Vector3(3.6f, 0.9f, -70f), new Vector3(47.8f, 1.8f, 0.1f), metalMat, root);
        B.CreateBox("Fence_SouthE", new Vector3(61.4f, 0.9f, -70f), new Vector3(57.8f, 1.8f, 0.1f), metalMat, root);
        B.CreateBox("Fence_West", new Vector3(-20f, 0.9f, -39f), new Vector3(0.1f, 1.8f, 62f), metalMat, root);
        B.CreateBox("Fence_East", new Vector3(90f, 0.9f, -30f), new Vector3(0.1f, 1.8f, 80f), metalMat, root);
        B.CreateBox("Fence_North", new Vector3(75.15f, 0.9f, 10f), new Vector3(29.7f, 1.8f, 0.1f), metalMat, root);
        B.CreateBox("Fence_BehindBuilding", new Vector3(60.3f, 0.9f, 6.6f), new Vector3(0.1f, 1.8f, 6.8f), metalMat, root);
        return gate;
    }

    // ───────────── 1階 職員室 ─────────────

    /// <summary>先生の机が並んでいる。担任の机の引き出しに、渡り廊下の鍵がある</summary>
    static void BuildStaffRoom(Transform environment)
    {
        var root = Group("StaffRoom_1F", environment);
        var steelMat = B.GetOrCreateMaterial("SteelDesk", new Color(0.6f, 0.62f, 0.6f));
        var boardMat = B.GetOrCreateMaterial("Whiteboard", new Color(0.95f, 0.95f, 0.95f));
        var keyBoxMat = B.GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f));
        var paperMat = B.GetOrCreateMaterial("Paper", new Color(0.95f, 0.94f, 0.9f));
        var silverMat = B.GetOrCreateMaterial("Silver", new Color(0.75f, 0.77f, 0.8f));
        float x0 = RoomX0(StaffRoom);
        float y = FloorY(0);

        var whiteboard = B.CreateBox("Whiteboard", new Vector3(x0 + 0.13f, y + 1.5f, -5.5f), new Vector3(0.05f, 1.1f, 2.4f), boardMat, root);
        whiteboard.AddComponent<InfoSign>().Setup("ホワイトボード", "今日の予定が書かれている。\n「6/14　2年3組　面談」……消されずに残っている。");
        var keyBox = B.CreateBox("KeyBox", new Vector3(x0 + 0.13f, y + 1.5f, -2f), new Vector3(0.08f, 0.5f, 0.4f), keyBoxMat, root);
        keyBox.AddComponent<InfoSign>().Setup("キーボックス", "キーボックス。4桁の番号の鍵がかかっている。", "調べる");

        // 先生の机：向かい合わせの島が2つ
        string[] contents =
        {
            "テストの答案の束が入っている。", "書類が入っている。", "何も入っていない。", "お菓子が入っている。",
            "……出席簿。相沢の欄だけ、ずっと空白だ。", "何も入っていない。", "書類が入っている。", "赤ペンが一本だけ入っている。",
        };
        float[] rows = { -3f, -3.75f, -5.8f, -6.55f };
        float[] columns = { x0 + 2.2f, x0 + 3.5f, x0 + 4.8f, x0 + 6.1f };
        int index = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < columns.Length; c++)
            {
                var desk = B.CreateBox($"TeacherDesk_{index}", new Vector3(columns[c], y + 0.36f, rows[r]), new Vector3(1.2f, 0.72f, 0.7f), steelMat, root);
                bool isHomeroomDesk = r == 0 && c == 2;
                if (isHomeroomDesk)
                {
                    // 担任の机：引き出しに渡り廊下の鍵、上に手紙④
                    var key = CreateKey("Key_Walkway", new Vector3(columns[c], y + 0.6f, rows[r]), silverMat, WalkwayKeyId,
                        "渡り廊下の鍵", "「渡り廊下」と書かれたタグがついている。体育館へ行けそうだ。", root);
                    desk.AddComponent<ItemContainer>().Setup("担任の机の引き出し", key, "引き出しには、もう何も入っていない。");

                    var letter = B.CreateBox("Letter_04", new Vector3(columns[c] + 0.2f, y + 0.721f, rows[r]), new Vector3(0.15f, 0.002f, 0.21f), paperMat, root);
                    letter.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
                    letter.isStatic = false;
                    letter.AddComponent<ItemPickup>().Setup("letter_04", "手紙", "担任の机の上に置かれていた紙。", Letter04);
                }
                else
                {
                    desk.AddComponent<ItemContainer>().Setup("先生の机", null, contents[index % contents.Length]);
                }
                index++;
            }
        }
    }

    // ───────────── 渡り廊下と体育館 ─────────────

    /// <summary>
    /// 校舎1階の東の端 → 渡り廊下 → 体育館。体育館の中の体育倉庫に校門の鍵がある。
    /// クライマックスで相沢が現れる場所（体育館の南の出口の内側）を返す。
    /// </summary>
    static Transform BuildWalkwayAndGym(Transform environment)
    {
        var root = Group("WalkwayAndGym", environment);
        var wallMat = WallMaterial();
        var roofMat = B.GetOrCreateMaterial("GymRoof", new Color(0.35f, 0.4f, 0.45f));
        var gymFloorMat = B.GetOrCreateMaterial("GymFloor", new Color(0.72f, 0.55f, 0.35f));
        var pillarMat = B.GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f));
        var stageMat = B.GetOrCreateMaterial("Wood", new Color(0.7f, 0.55f, 0.38f));
        var matMat = B.GetOrCreateMaterial("GymMat", new Color(0.3f, 0.45f, 0.7f));
        var basketMat = B.GetOrCreateMaterial("BallBasket", new Color(0.35f, 0.35f, 0.3f));
        var ironMat = B.GetOrCreateMaterial("Iron", new Color(0.25f, 0.25f, 0.27f));
        var paperMat = B.GetOrCreateMaterial("Paper", new Color(0.95f, 0.94f, 0.9f));

        // 校舎1階の突き当たりの扉（鍵がかかっている）。扉は校舎側にあり、南へスライドして開く
        var walkwayDoor = new GameObject("Door_Walkway");
        walkwayDoor.transform.SetParent(root, false);
        walkwayDoor.transform.SetPositionAndRotation(new Vector3(BuildingLength + 0.1f, 0f, 1.5f), Quaternion.Euler(0f, 90f, 0f));
        var walkwayPanel = B.CreateBox("Panel", new Vector3(0f, 1f, -0.13f), new Vector3(1f, 2f, 0.04f), DoorMaterial(), walkwayDoor.transform, true);
        walkwayPanel.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        walkwayPanel.AddComponent<NavMeshObstacle>().carving = true;
        walkwayDoor.AddComponent<Door>().Setup("渡り廊下への扉", Door.DoorType.Sliding, walkwayPanel.transform, WalkwayKeyId,
            "渡り廊下への扉。鍵がかかっている。\n鍵は……職員室か？");

        // 渡り廊下（x 60.2〜64、屋根と低い壁。横からは出入りできない）
        B.CreateBox("Walkway_Roof", new Vector3(62.1f, 2.8f, 1.5f), new Vector3(4f, 0.2f, 3.6f), roofMat, root);
        foreach (float z in new[] { -0.05f, 3.05f })
        {
            B.CreateBox($"Walkway_LowWall_{z}", new Vector3(62.1f, 0.5f, z), new Vector3(3.8f, 1f, 0.1f), wallMat, root);
            var guard = new GameObject($"Walkway_Guard_{z}") { layer = 2 };
            guard.transform.SetParent(root, false);
            guard.transform.position = new Vector3(62.1f, 1.5f, z);
            guard.AddComponent<BoxCollider>().size = new Vector3(3.8f, 3f, 0.1f);
            foreach (float x in new[] { 60.4f, 63.8f })
                B.CreateBox($"Walkway_Pillar_{x}_{z}", new Vector3(x, 1.4f, z), new Vector3(0.12f, 2.8f, 0.12f), pillarMat, root);
        }

        // 体育館（x 64〜88、z -14〜6、高さ8m）。西に渡り廊下からの入口、南に校庭への出口
        const float gymHeight = 8f;
        B.CreateBox("Gym_Floor", new Vector3(76f, -0.04f, -4f), new Vector3(24f, 0.12f, 20f), gymFloorMat, root);
        B.CreateBox("Gym_Roof", new Vector3(76f, gymHeight + 0.3f, -4f), new Vector3(24.6f, 0.6f, 20.6f), roofMat, root);
        B.CreateBox("Gym_Wall_North", new Vector3(76f, gymHeight / 2f, 6f), new Vector3(24f, gymHeight, 0.3f), wallMat, root);
        B.CreateBox("Gym_Wall_East", new Vector3(88f, gymHeight / 2f, -4f), new Vector3(0.3f, gymHeight, 20.3f), wallMat, root);
        B.CreateBox("Gym_Wall_West_South", new Vector3(64f, gymHeight / 2f, -6.5f), new Vector3(0.3f, gymHeight, 15f), wallMat, root);
        B.CreateBox("Gym_Wall_West_North", new Vector3(64f, gymHeight / 2f, 4f), new Vector3(0.3f, gymHeight, 4f), wallMat, root);
        B.CreateBox("Gym_Wall_West_Top", new Vector3(64f, (2.2f + gymHeight) / 2f, 1.5f), new Vector3(0.3f, gymHeight - 2.2f, 1f), wallMat, root);
        B.CreateBox("Gym_Wall_South_West", new Vector3(69.5f, gymHeight / 2f, -14f), new Vector3(11f, gymHeight, 0.3f), wallMat, root);
        B.CreateBox("Gym_Wall_South_East", new Vector3(82.5f, gymHeight / 2f, -14f), new Vector3(11f, gymHeight, 0.3f), wallMat, root);
        B.CreateBox("Gym_Wall_South_Top", new Vector3(76f, (2.4f + gymHeight) / 2f, -14f), new Vector3(2f, gymHeight - 2.4f, 0.3f), wallMat, root);

        // ステージ（東側）と手紙⑧
        B.CreateBox("Stage", new Vector3(85.5f, 0.5f, -4f), new Vector3(5f, 1f, 16f), stageMat, root);
        var letter08 = B.CreateBox("Letter_08", new Vector3(83.4f, 1.001f, -3f), new Vector3(0.15f, 0.002f, 0.21f), paperMat, root);
        letter08.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
        letter08.isStatic = false;
        letter08.AddComponent<ItemPickup>().Setup("letter_08", "手紙", "ステージの端に置かれていた紙。", Letter08);

        // 体育倉庫（北側の真ん中、x 70〜76、z 1.5〜6）。中のボールかごに校門の鍵、マットの上に手紙⑨
        B.CreateBox("Storage_Wall_West", new Vector3(70f, 1.5f, 3.75f), new Vector3(0.2f, 3f, 4.5f), wallMat, root);
        B.CreateBox("Storage_Wall_East", new Vector3(76f, 1.5f, 3.75f), new Vector3(0.2f, 3f, 4.5f), wallMat, root);
        B.CreateBox("Storage_Wall_South_W", new Vector3(71.25f, 1.5f, 1.5f), new Vector3(2.5f, 3f, 0.2f), wallMat, root);
        B.CreateBox("Storage_Wall_South_E", new Vector3(74.75f, 1.5f, 1.5f), new Vector3(2.5f, 3f, 0.2f), wallMat, root);
        B.CreateBox("Storage_Wall_South_Top", new Vector3(73f, 2.5f, 1.5f), new Vector3(1f, 1f, 0.2f), wallMat, root);
        B.CreateBox("Storage_Ceiling", new Vector3(73f, 3.1f, 3.75f), new Vector3(6.2f, 0.2f, 4.7f), wallMat, root);
        CreateSlidingDoor("Door_Storage", 73f, 0f, "体育倉庫の扉", "", "", root, 1.5f);

        var gateKey = CreateKey("Key_Gate", new Vector3(74.6f, 0.5f, 4.6f), ironMat, GateKeyId,
            "校門の鍵", "大きくて重い、鉄の鍵。これで校門が開くはずだ。", root, 1.8f);
        var basket = B.CreateBox("BallBasket", new Vector3(74.6f, 0.45f, 4.6f), new Vector3(0.9f, 0.9f, 0.9f), basketMat, root);
        basket.AddComponent<ItemContainer>().Setup("ボールかご", gateKey, "ボールしか入っていない。");

        B.CreateBox("Mats", new Vector3(71.6f, 0.2f, 4.5f), new Vector3(1.8f, 0.4f, 1.1f), matMat, root);
        var letter09 = B.CreateBox("Letter_09", new Vector3(71.4f, 0.401f, 4.4f), new Vector3(0.15f, 0.002f, 0.21f), paperMat, root);
        letter09.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
        letter09.isStatic = false;
        letter09.AddComponent<ItemPickup>().Setup("letter_09", "手紙", "マットの上に置かれていた紙。", Letter09);

        // 体育館の中は暗い。天井の照明が2つだけ、弱くついている
        foreach (float x in new[] { 70f, 82f })
        {
            var lightObject = new GameObject($"GymLight_{x}");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.position = new Vector3(x, gymHeight - 1f, -6f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.85f, 0.9f, 1f);
            light.intensity = 1.2f;
            light.range = 14f;
        }

        return CreateMarker("AizawaAppearPoint", new Vector3(76f, 0f, -12.5f), root);
    }

    /// <summary>鍵（持ち手＋軸＋歯）。最初は非表示で、入れ物（机・かご）から出てくる</summary>
    static ItemPickup CreateKey(string name, Vector3 position, Material material, string id, string displayName, string description, Transform parent, float scale = 1f)
    {
        var key = new GameObject(name);
        key.transform.SetParent(parent, false);
        key.transform.position = position;
        key.transform.localScale = Vector3.one * scale;
        B.CreateBox("Shaft", new Vector3(0f, 0f, 0.02f), new Vector3(0.012f, 0.004f, 0.06f), material, key.transform, true);
        B.CreateBox("Bow", new Vector3(0f, 0f, -0.025f), new Vector3(0.035f, 0.005f, 0.03f), material, key.transform, true);
        B.CreateBox("Teeth", new Vector3(0.01f, 0f, 0.045f), new Vector3(0.012f, 0.004f, 0.012f), material, key.transform, true);
        var pickup = key.AddComponent<ItemPickup>();
        pickup.Setup(id, displayName, description);
        key.SetActive(false);
        return pickup;
    }

    static T CreateZone<T>(string name, Vector3 center, Vector3 size, PlayerController player) where T : PlayerZone
    {
        var zone = new GameObject(name).AddComponent<T>();
        zone.transform.position = center;
        zone.SetupZone(player, size);
        return zone;
    }

    // ───────────── 部品 ─────────────

    /// <summary>引き戸。panel は廊下側に置き、右（東）へスライドして開く。keyId が空なら鍵なし</summary>
    static void CreateSlidingDoor(string name, float x, float y, string doorName, string keyId, string lockedMessage, Transform parent, float z = 0f)
    {
        var door = new GameObject(name);
        door.transform.SetParent(parent, false);
        door.transform.position = new Vector3(x, y, z);

        var panel = B.CreateBox("Panel", new Vector3(x, y + 1f, z + 0.13f), new Vector3(1f, 2f, 0.04f), DoorMaterial(), door.transform);
        panel.isStatic = false;
        panel.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        panel.AddComponent<NavMeshObstacle>().carving = true;
        door.AddComponent<Door>().Setup(doorName, Door.DoorType.Sliding, panel.transform, keyId,
            string.IsNullOrEmpty(lockedMessage) ? "鍵がかかっている。" : lockedMessage);
    }

    /// <summary>壁に貼り付けただけの、開かない扉</summary>
    static void CreateFakeDoor(string name, float x, float y, string doorName, string message, Transform parent)
    {
        var panel = B.CreateBox(name, new Vector3(x, y + 1f, 0.13f), new Vector3(1f, 2f, 0.04f), DoorMaterial(), parent);
        panel.AddComponent<Door>().Setup(doorName, Door.DoorType.Sliding, panel.transform, NeverOpens, message);
    }

    /// <summary>扉の上に廊下へ突き出している室名札</summary>
    static void CreateRoomPlate(float x, float y, string roomName, Transform parent)
    {
        var plateMat = B.GetOrCreateMaterial("RoomPlate", new Color(0.95f, 0.95f, 0.92f));
        var plate = B.CreateBox($"Plate_{roomName}", new Vector3(x, y + 2.45f, 0.33f), new Vector3(0.04f, 0.15f, 0.45f), plateMat, parent);
        plate.AddComponent<InfoSign>().Setup("室名札", $"「{roomName}」");
    }

    /// <summary>東西方向（x）にのびる壁</summary>
    static void WallX(string name, float xFrom, float xTo, float z, float yFrom, float yTo, Material material, Transform parent)
    {
        if (xTo - xFrom < 0.01f) return;
        B.CreateBox(name, new Vector3((xFrom + xTo) / 2f, (yFrom + yTo) / 2f, z), new Vector3(xTo - xFrom, yTo - yFrom, 0.2f), material, parent);
    }

    static Material WallMaterial() => B.GetOrCreateMaterial("Wall", new Color(0.85f, 0.85f, 0.8f));
    static Material FloorMaterial() => B.GetOrCreateMaterial("Floor", new Color(0.55f, 0.45f, 0.35f));
    static Material DoorMaterial() => B.GetOrCreateMaterial("Door", new Color(0.62f, 0.5f, 0.36f));

    static Transform Group(string name, Transform parent)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    static Transform CreateMarker(string name, Vector3 position, Transform parent)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.position = position;
        return marker;
    }

    static void SetArray(Object target, string propertyName, Object[] values)
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(propertyName);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
