using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// メニュー「放課後 > プレイヤーのテストシーンを作成」で、
/// プレイヤーの動き・調べる・鍵とドアを確かめるためのテスト用シーンを自動で作る。
/// </summary>
public static class PrototypeSceneBuilder
{
    const string ScenePath = "Assets/Scenes/PlayerTest.unity";
    const string MaterialFolder = "Assets/_Project/Materials";
    const string ClassroomKeyId = "key_2-3";

    const string Letter01 =
        "今日も俺の机だけ離れていた。\n誰が動かしたのかは知ってる。\nでも、誰も元に戻さなかった。";

    const string FriendChatLog =
        "［2年3組 男子］\n\n" +
        "佐藤：明日あいつの机どうする？\n" +
        "山本：また離しとこうぜ\n" +
        "佐藤：高橋もそれでいいよな\n" +
        "高橋：まあ、いいんじゃね\n\n" +
        "――相沢 悠 はこのグループに参加していません";

    [MenuItem("放課後/プレイヤーのテストシーンを作成")]
    static void CreatePlayerTestScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("確認", $"{ScenePath} はすでにあります。作り直しますか？", "作り直す", "やめる"))
            return;

        int firstPersonLayer = EnsureLayer(FirstPersonLayer.Name);
        EnsureNavMeshAgentSize();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // プレイヤーにカメラを持たせるので、最初からあるカメラは消す
        var defaultCamera = Object.FindAnyObjectByType<Camera>();
        if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

        // 夕日っぽい低い角度のオレンジ色の光
        var sun = Object.FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.color = new Color(1f, 0.62f, 0.35f);
            sun.intensity = 1.5f;
            sun.transform.rotation = Quaternion.Euler(15f, -60f, 0f);
        }

        var environment = new GameObject("Environment").transform;
        BuildTestArea(environment);
        BuildClassroom(environment);

        // 相沢が歩ける範囲は、ゲーム開始時に Environment の中から自動で作る
        var surface = environment.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        environment.gameObject.AddComponent<RuntimeNavMeshBaker>();

        var player = BuildPlayer(new Vector3(-6.5f, 0f, 2.5f), firstPersonLayer);
        var playerController = player.GetComponent<PlayerController>();
        var playerCamera = player.GetComponentInChildren<Camera>();

        var gameOver = new GameObject("GameOver").AddComponent<GameOver>();
        SetReference(gameOver, "player", playerController);
        SetReference(gameOver, "playerCamera", playerCamera);

        // 巡回ルート：教室の外を一周する
        Vector3[] patrol =
        {
            new(7f, 0f, 8f), new(7f, 0f, -8f), new(-4f, 0f, -8f), new(-1.5f, 0f, -2f), new(-1.5f, 0f, 8f),
        };
        BuildAizawa(new Vector3(7f, 0f, 8f), 180f, patrol, playerController, playerCamera.transform, gameOver);

        Selection.activeGameObject = player;
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        Debug.Log($"テストシーンを作成しました: {ScenePath}　再生ボタンを押して動かしてみてください。");
    }

    // ───────────── 動きの確認用エリア ─────────────

    static void BuildTestArea(Transform root)
    {
        var floorMat = GetOrCreateMaterial("Floor", new Color(0.55f, 0.45f, 0.35f));
        var wallMat = GetOrCreateMaterial("Wall", new Color(0.85f, 0.85f, 0.8f));
        var propMat = GetOrCreateMaterial("Prop", new Color(0.4f, 0.5f, 0.6f));

        // 床と外壁（20m × 20m、高さ3m）
        CreateBox("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f), floorMat, root);
        CreateBox("Wall_North", new Vector3(0f, 1.5f, 10f), new Vector3(20f, 3f, 0.2f), wallMat, root);
        CreateBox("Wall_South", new Vector3(0f, 1.5f, -10f), new Vector3(20f, 3f, 0.2f), wallMat, root);
        CreateBox("Wall_East", new Vector3(10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f), wallMat, root);
        CreateBox("Wall_West", new Vector3(-10f, 1.5f, 0f), new Vector3(0.2f, 3f, 20f), wallMat, root);

        // 廊下と同じ幅（3m）の通路。Corridor_A は教室の東の壁も兼ねる
        CreateBox("Corridor_A", new Vector3(-3f, 1.5f, 4.5f), new Vector3(0.2f, 3f, 11f), wallMat, root);
        CreateBox("Corridor_B", new Vector3(0f, 1.5f, 4f), new Vector3(0.2f, 3f, 10f), wallMat, root);

        // しゃがまないと通れない低い隙間（下の高さ1.2m）。教卓や机の下の代わり
        CreateBox("LowGap", new Vector3(5f, 1.45f, -5f), new Vector3(3f, 0.5f, 3f), propMat, root);

        // ジャンプで乗れる段差（高さ0.5m）
        CreateBox("JumpBox", new Vector3(5f, 0.25f, 5f), new Vector3(2f, 0.5f, 2f), propMat, root);

        // 階段（1段 0.15m × 10段）。学校の階段の上り下り確認用
        for (int i = 0; i < 10; i++)
        {
            float height = 0.15f * (i + 1);
            CreateBox($"Step_{i + 1:00}", new Vector3(-7f, height / 2f, -6f + 0.3f * i), new Vector3(2f, height, 0.3f), propMat, root);
        }
        CreateBox("Landing", new Vector3(-7f, 0.75f, -2.15f), new Vector3(2f, 1.5f, 2f), propMat, root);
    }

    // ───────────── 閉じ込められた教室（2年3組） ─────────────

    static void BuildClassroom(Transform environment)
    {
        var wallMat = GetOrCreateMaterial("Wall", new Color(0.85f, 0.85f, 0.8f));
        var doorMat = GetOrCreateMaterial("Door", new Color(0.62f, 0.5f, 0.36f));
        var boardMat = GetOrCreateMaterial("Blackboard", new Color(0.12f, 0.25f, 0.18f));
        var goldMat = GetOrCreateMaterial("Gold", new Color(0.85f, 0.7f, 0.3f));
        var paperMat = GetOrCreateMaterial("Paper", new Color(0.95f, 0.94f, 0.9f));
        var phoneMat = GetOrCreateMaterial("Phone", new Color(0.08f, 0.08f, 0.1f));

        var root = new GameObject("Classroom_2-3").transform;
        root.SetParent(environment, false);

        // 教室は x -10〜-3、z 1〜10。南の壁に引き戸がある
        CreateBox("Wall_South_A", new Vector3(-7.75f, 1.5f, 1f), new Vector3(4.5f, 3f, 0.2f), wallMat, root);
        CreateBox("Wall_South_B", new Vector3(-3.75f, 1.5f, 1f), new Vector3(1.5f, 3f, 0.2f), wallMat, root);
        CreateBox("Wall_South_Top", new Vector3(-5f, 2.5f, 1f), new Vector3(1f, 1f, 0.2f), wallMat, root);
        CreateBox("Blackboard", new Vector3(-6.5f, 1.5f, 9.85f), new Vector3(4f, 1.2f, 0.05f), boardMat, root);

        // 引き戸（鍵がかかっている）。右にスライドして開く
        var door = new GameObject("Door_2-3");
        door.transform.SetParent(root, false);
        door.transform.position = new Vector3(-5f, 0f, 1f);
        var panel = CreateBox("Panel", new Vector3(-5f, 1f, 0.87f), new Vector3(1f, 2f, 0.04f), doorMat, door.transform);
        panel.isStatic = false;
        // 扉はナビメッシュに焼き込まず、閉まっている間だけ相沢の通り道をふさぐ
        panel.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        panel.AddComponent<NavMeshObstacle>().carving = true;
        door.AddComponent<Door>().Setup("2年3組の扉", Door.DoorType.Sliding, panel.transform, ClassroomKeyId,
            "鍵がかかっている。……外から？\nどこかに鍵はないか。");

        // 並んだ机（3列 × 3行）。どの机も中を調べられる
        string[] deskContents =
        {
            "教科書とノートが入っている。",
            "友人の教科書が入っている。",   // スマートフォンが置いてある友人の机
            "丸めたプリントが押し込まれている。",
            "何も入っていない。",
            "お菓子の空き袋が入っている。",
            "体操服の袋が入っている。",
            "何も入っていない。",
            "辞書が一冊だけ入っている。",
            "落書きだらけのノートが入っている。",
        };
        float[] columns = { -8.2f, -6.5f, -4.8f };
        float[] rows = { 5f, 6.5f, 8f };
        int deskIndex = 0;
        foreach (float z in rows)
        {
            foreach (float x in columns)
            {
                var desk = CreateDesk($"Desk_{deskIndex + 1}", new Vector3(x, 0f, z), root);
                desk.AddComponent<ItemContainer>().Setup("机", null, deskContents[deskIndex]);
                deskIndex++;
            }
        }

        // 友人の机の上に残されたスマートフォン
        var phone = CreateBox("FriendPhone", new Vector3(-6.5f, 0.74f, 5f), new Vector3(0.07f, 0.008f, 0.15f), phoneMat, root);
        phone.isStatic = false;
        phone.AddComponent<ItemPickup>().Setup("phone_friend", "友人のスマートフォン",
            "さっきまで話していた友人のものだ。画面がついたままになっている。", FriendChatLog);

        // 相沢の机：ほかの机から少し離れている。中に教室の鍵、上に手紙
        var aizawaDesk = CreateDesk("Desk_Aizawa", new Vector3(-9.2f, 0f, 8.8f), root);

        var letter = CreateBox("Letter_01", new Vector3(-9.2f, 0.737f, 8.8f), new Vector3(0.15f, 0.002f, 0.21f), paperMat, root);
        letter.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
        letter.isStatic = false;
        letter.AddComponent<ItemPickup>().Setup("letter_01", "手紙", "相沢の机の上に置かれていた紙。", Letter01);

        var key = new GameObject("Key_2-3");
        key.transform.SetParent(root, false);
        key.transform.position = new Vector3(-9.2f, 0.64f, 8.8f);
        CreateBox("Shaft", Vector3.zero, new Vector3(0.012f, 0.004f, 0.06f), goldMat, key.transform, true).transform.localPosition = new Vector3(0f, 0f, 0.02f);
        CreateBox("Bow", Vector3.zero, new Vector3(0.035f, 0.005f, 0.03f), goldMat, key.transform, true).transform.localPosition = new Vector3(0f, 0f, -0.025f);
        CreateBox("Teeth", Vector3.zero, new Vector3(0.012f, 0.004f, 0.012f), goldMat, key.transform, true).transform.localPosition = new Vector3(0.01f, 0f, 0.045f);
        var keyPickup = key.AddComponent<ItemPickup>();
        keyPickup.Setup(ClassroomKeyId, "2年3組の鍵", "教室の扉の鍵。なぜ相沢の机の中に？");

        aizawaDesk.AddComponent<ItemContainer>().Setup("机", keyPickup, "もう何も入っていない。");
        key.SetActive(false);
    }

    internal static GameObject CreateDesk(string name, Vector3 position, Transform parent)
    {
        var woodMat = GetOrCreateMaterial("Wood", new Color(0.7f, 0.55f, 0.38f));
        var metalMat = GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f));

        var desk = new GameObject(name);
        desk.transform.SetParent(parent, false);
        desk.transform.position = position;

        CreateBox("Top", Vector3.zero, new Vector3(0.65f, 0.03f, 0.45f), woodMat, desk.transform).transform.localPosition = new Vector3(0f, 0.72f, 0f);
        CreateBox("Box", Vector3.zero, new Vector3(0.6f, 0.14f, 0.4f), metalMat, desk.transform).transform.localPosition = new Vector3(0f, 0.63f, 0f);
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -0.28f : 0.28f;
            float z = i < 2 ? -0.18f : 0.18f;
            CreateBox($"Leg_{i}", Vector3.zero, new Vector3(0.03f, 0.6f, 0.03f), metalMat, desk.transform).transform.localPosition = new Vector3(x, 0.3f, z);
        }
        return desk;
    }

    // ───────────── プレイヤー ─────────────

    internal static GameObject BuildPlayer(Vector3 position, int firstPersonLayer)
    {
        var player = new GameObject("Player");
        player.transform.position = position;

        var controller = player.AddComponent<CharacterController>();
        controller.height = 1.7f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.85f, 0f);
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.03f;
        controller.slopeLimit = 45f;

        // 世界を映すカメラ（手は映さない）
        var cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var mainCamera = cameraObject.AddComponent<Camera>();
        mainCamera.nearClipPlane = 0.05f;
        mainCamera.fieldOfView = 60f;
        mainCamera.cullingMask = ~(1 << firstPersonLayer);
        cameraObject.AddComponent<AudioListener>();

        // 手とアイテムだけを一番手前に映すカメラ（壁にめり込まないようにする）
        var armsCameraObject = new GameObject("ArmsCamera");
        armsCameraObject.transform.SetParent(cameraObject.transform, false);
        var armsCamera = armsCameraObject.AddComponent<Camera>();
        armsCamera.nearClipPlane = 0.01f;
        armsCamera.farClipPlane = 3f;
        armsCamera.fieldOfView = 60f;
        armsCamera.cullingMask = 1 << firstPersonLayer;
        armsCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        mainCamera.GetUniversalAdditionalCameraData().cameraStack.Add(armsCamera);

        var playerController = player.AddComponent<PlayerController>();
        SetReference(playerController, "cameraRoot", cameraObject.transform);

        var inventory = player.AddComponent<Inventory>();
        SetReference(inventory, "player", playerController);

        var interactor = player.AddComponent<Interactor>();
        var arms = BuildArms(cameraObject.transform, firstPersonLayer);
        SetReference(interactor, "playerCamera", mainCamera);
        SetReference(interactor, "player", playerController);
        SetReference(interactor, "inventory", inventory);
        SetReference(interactor, "arms", arms);

        SetReference(arms, "player", playerController);
        SetReference(arms, "interactor", interactor);
        SetReference(arms, "inventory", inventory);

        var hiding = player.AddComponent<PlayerHiding>();
        SetReference(hiding, "player", playerController);

        var hud = player.AddComponent<PlayerHUD>();
        SetReference(hud, "player", playerController);
        SetReference(hud, "interactor", interactor);
        SetReference(hud, "inventory", inventory);

        return player;
    }

    static FirstPersonArms BuildArms(Transform cameraTransform, int firstPersonLayer)
    {
        var sleeveMat = GetOrCreateMaterial("Sleeve", new Color(0.1f, 0.12f, 0.2f));
        var skinMat = GetOrCreateMaterial("Skin", new Color(0.93f, 0.78f, 0.66f));

        var root = new GameObject("Arms");
        root.transform.SetParent(cameraTransform, false);

        var leftArm = CreateArm("LeftArm", root.transform, new Vector3(-0.2f, -0.35f, -0.05f), sleeveMat, skinMat);
        var rightArm = CreateArm("RightArm", root.transform, new Vector3(0.2f, -0.35f, -0.05f), sleeveMat, skinMat);

        var handAnchor = new GameObject("HandAnchor").transform;
        handAnchor.SetParent(rightArm, false);
        handAnchor.localPosition = new Vector3(0f, 0.045f, 0.5f);

        SetLayerRecursive(root, firstPersonLayer);

        var arms = root.AddComponent<FirstPersonArms>();
        SetReference(arms, "leftArm", leftArm);
        SetReference(arms, "rightArm", rightArm);
        SetReference(arms, "handAnchor", handAnchor);
        return arms;
    }

    /// <summary>肩の位置を中心に回る腕。袖（制服）と手でできている</summary>
    static Transform CreateArm(string name, Transform parent, Vector3 shoulder, Material sleeveMat, Material skinMat)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = shoulder;
        pivot.localRotation = Quaternion.Euler(-15f, 0f, 0f);

        var sleeve = CreateBox("Sleeve", Vector3.zero, new Vector3(0.09f, 0.09f, 0.44f), sleeveMat, pivot, true);
        sleeve.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        var hand = CreateBox("Hand", Vector3.zero, new Vector3(0.075f, 0.05f, 0.1f), skinMat, pivot, true);
        hand.transform.localPosition = new Vector3(0f, 0f, 0.49f);

        foreach (var box in new[] { sleeve, hand })
        {
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            box.isStatic = false;
        }
        return pivot;
    }

    // ───────────── 相沢 ─────────────

    /// <summary>
    /// 仮の人の形。当たり判定はなし。
    /// 友人：血色のいい肌・短い髪・白いシャツの襟・まっすぐ立つ。
    /// 相沢：青白い肌・目にかかる長い前髪・前髪の奥で光る目・猫背で首をかしげ、腕をだらんと下げている。
    /// </summary>
    internal static GameObject CreateFigure(string name, Vector3 position, float yaw, bool isAizawa)
    {
        var uniformMat = isAizawa
            ? GetOrCreateMaterial("AizawaUniform", new Color(0.13f, 0.13f, 0.15f))
            : GetOrCreateMaterial("Sleeve", new Color(0.1f, 0.12f, 0.2f));
        var skinMat = isAizawa
            ? GetOrCreateMaterial("AizawaSkin", new Color(0.72f, 0.74f, 0.74f))
            : GetOrCreateMaterial("Skin", new Color(0.93f, 0.78f, 0.66f));
        var hairMat = GetOrCreateMaterial("Hair", new Color(0.04f, 0.04f, 0.05f));
        var shirtMat = GetOrCreateMaterial("Shirt", new Color(0.95f, 0.95f, 0.95f));
        var eyeMat = isAizawa ? GetOrCreateGlowMaterial("AizawaEyeGlow", new Color(0.9f, 0.85f, 0.8f), 2f)
                              : GetOrCreateMaterial("AizawaEye", new Color(0.02f, 0.02f, 0.02f));

        var figure = new GameObject(name);
        figure.transform.position = position;
        figure.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // 相沢は全体を少し前に傾けて猫背にする
        var pose = new GameObject("Pose").transform;
        pose.SetParent(figure.transform, false);
        if (isAizawa) pose.localRotation = Quaternion.Euler(7f, 0f, 0f);

        CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.75f, 0f), new Vector3(0.4f, 0.7f, 0.3f), uniformMat, pose);
        float armLength = isAizawa ? 0.4f : 0.33f;
        float armTilt = isAizawa ? 4f : 10f;
        var leftArm = CreatePart(PrimitiveType.Capsule, "Arm_L", new Vector3(-0.25f, 1.25f - armLength, 0f), new Vector3(0.1f, armLength, 0.1f), uniformMat, pose);
        leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, -armTilt);
        var rightArm = CreatePart(PrimitiveType.Capsule, "Arm_R", new Vector3(0.25f, 1.25f - armLength, 0f), new Vector3(0.1f, armLength, 0.1f), uniformMat, pose);
        rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, armTilt);

        // 首を中心に頭を傾ける
        var headPivot = new GameObject("HeadPivot").transform;
        headPivot.SetParent(pose, false);
        headPivot.localPosition = new Vector3(0f, 1.48f, 0f);
        if (isAizawa) headPivot.localRotation = Quaternion.Euler(12f, 0f, 16f);

        CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.14f, 0f), Vector3.one * 0.24f, skinMat, headPivot);
        CreatePart(PrimitiveType.Sphere, "Eye_L", new Vector3(-0.045f, 0.15f, 0.105f), Vector3.one * 0.03f, eyeMat, headPivot);
        CreatePart(PrimitiveType.Sphere, "Eye_R", new Vector3(0.045f, 0.15f, 0.105f), Vector3.one * 0.03f, eyeMat, headPivot);

        if (isAizawa)
        {
            // 目にかかる長い前髪と、肩まである横の髪
            CreatePart(PrimitiveType.Sphere, "Hair_Top", new Vector3(0f, 0.2f, -0.01f), new Vector3(0.27f, 0.22f, 0.27f), hairMat, headPivot);
            CreatePart(PrimitiveType.Cube, "Hair_Bangs", new Vector3(0f, 0.19f, 0.11f), new Vector3(0.22f, 0.1f, 0.04f), hairMat, headPivot);
            CreatePart(PrimitiveType.Cube, "Hair_Side_L", new Vector3(-0.12f, 0.02f, 0f), new Vector3(0.05f, 0.32f, 0.2f), hairMat, headPivot);
            CreatePart(PrimitiveType.Cube, "Hair_Side_R", new Vector3(0.12f, 0.02f, 0f), new Vector3(0.05f, 0.32f, 0.2f), hairMat, headPivot);
            CreatePart(PrimitiveType.Cube, "Hair_Back", new Vector3(0f, 0.02f, -0.1f), new Vector3(0.22f, 0.32f, 0.05f), hairMat, headPivot);
        }
        else
        {
            // 短い髪と、白いシャツの襟
            CreatePart(PrimitiveType.Sphere, "Hair_Top", new Vector3(0f, 0.2f, -0.015f), new Vector3(0.26f, 0.17f, 0.26f), hairMat, headPivot);
            CreatePart(PrimitiveType.Cube, "Collar", new Vector3(0f, 1.46f, 0.07f), new Vector3(0.18f, 0.06f, 0.1f), shirtMat, pose);
        }
        return figure;
    }

    /// <summary>
    /// 掃除用具ロッカー（幅0.6m × 奥行0.55m × 高さ1.8m）。position は床の中心、yaw は扉が向く方向。
    /// 扉の上に横長のすき間（通気口）があり、中から外をのぞける。
    /// </summary>
    internal static GameObject CreateCleaningLocker(string name, Vector3 position, float yaw, Transform parent)
    {
        var lockerMat = GetOrCreateMaterial("Locker", new Color(0.55f, 0.6f, 0.55f));

        var locker = new GameObject(name);
        locker.transform.SetParent(parent, false);
        locker.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        var t = locker.transform;

        CreateBox("Back", new Vector3(0f, 0.9f, -0.265f), new Vector3(0.6f, 1.8f, 0.02f), lockerMat, t, true);
        CreateBox("Side_L", new Vector3(-0.29f, 0.9f, 0f), new Vector3(0.02f, 1.8f, 0.55f), lockerMat, t, true);
        CreateBox("Side_R", new Vector3(0.29f, 0.9f, 0f), new Vector3(0.02f, 1.8f, 0.55f), lockerMat, t, true);
        CreateBox("Top", new Vector3(0f, 1.79f, 0f), new Vector3(0.6f, 0.02f, 0.55f), lockerMat, t, true);
        CreateBox("Bottom", new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 0.02f, 0.55f), lockerMat, t, true);

        // 扉：下の板・通気口の横板（すき間からのぞく）・上の板
        CreateBox("Door_Lower", new Vector3(0f, 0.75f, 0.265f), new Vector3(0.6f, 1.5f, 0.02f), lockerMat, t, true);
        for (int i = 0; i < 4; i++)
            CreateBox($"Door_Slat_{i}", new Vector3(0f, 1.53f + 0.05f * i, 0.265f), new Vector3(0.56f, 0.018f, 0.02f), lockerMat, t, true);
        CreateBox("Door_Top", new Vector3(0f, 1.755f, 0.265f), new Vector3(0.6f, 0.07f, 0.02f), lockerMat, t, true);
        CreateBox("Handle", new Vector3(0.22f, 1.1f, 0.285f), new Vector3(0.02f, 0.12f, 0.02f), GetOrCreateMaterial("Metal", new Color(0.45f, 0.47f, 0.5f)), t, true);

        var hidePoint = new GameObject("HidePoint").transform;
        hidePoint.SetParent(t, false);
        hidePoint.localPosition = new Vector3(0f, 1.6f, 0.02f);

        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(t, false);
        exitPoint.localPosition = new Vector3(0f, 0f, 0.75f);

        locker.AddComponent<HidingSpot>().Setup("掃除用具ロッカー", hidePoint, exitPoint);
        return locker;
    }

    /// <summary>自分で光るマテリアル（暗い場所でも見える目など）</summary>
    static Material GetOrCreateGlowMaterial(string name, Color color, float intensity)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        material = GetOrCreateMaterial(name, color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * intensity);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    internal static GameObject BuildAizawa(Vector3 position, float yaw, Vector3[] points, PlayerController player, Transform playerEyes, GameOver gameOver)
    {
        var aizawa = CreateFigure("Aizawa", position, yaw, true);
        var head = aizawa.transform.Find("Pose/HeadPivot/Head");

        var eyes = new GameObject("Eyes").transform;
        eyes.SetParent(aizawa.transform, false);
        eyes.localPosition = new Vector3(0f, 1.64f, 0.12f);

        // ナビメッシュができるまで無効にしておく（AizawaAI が開始時に有効にする）
        var agent = aizawa.AddComponent<NavMeshAgent>();
        agent.radius = 0.3f;
        agent.height = 1.75f;
        agent.speed = 1.4f;
        agent.angularSpeed = 360f;
        agent.acceleration = 20f;
        agent.stoppingDistance = 0.2f;
        agent.enabled = false;

        aizawa.AddComponent<AudioSource>();
        aizawa.AddComponent<AizawaFootsteps>();

        var patrolRoot = new GameObject("AizawaPatrol").transform;
        var patrolPoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            patrolPoints[i] = new GameObject($"Point_{i + 1}").transform;
            patrolPoints[i].SetParent(patrolRoot, false);
            patrolPoints[i].position = points[i];
        }

        var ai = aizawa.AddComponent<AizawaAI>();
        SetReference(ai, "player", player);
        SetReference(ai, "playerEyes", playerEyes);
        SetReference(ai, "eyes", eyes);
        SetReference(ai, "face", head);
        SetReference(ai, "gameOver", gameOver);

        var serialized = new SerializedObject(ai);
        var array = serialized.FindProperty("patrolPoints");
        array.arraySize = patrolPoints.Length;
        for (int i = 0; i < patrolPoints.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = patrolPoints[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return aizawa;
    }

    internal static GameObject CreatePart(PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material, Transform parent)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        return part;
    }

    /// <summary>相沢の大きさに合わせて、ナビメッシュの歩く人の大きさを設定する（幅1mの扉を通れるように）</summary>
    internal static void EnsureNavMeshAgentSize()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var humanoid = settings.FindProperty("m_Settings").GetArrayElementAtIndex(0);
        humanoid.FindPropertyRelative("agentRadius").floatValue = 0.3f;
        humanoid.FindPropertyRelative("agentHeight").floatValue = 1.75f;
        humanoid.FindPropertyRelative("agentClimb").floatValue = 0.35f;
        settings.ApplyModifiedProperties();
    }

    internal static void AddSceneToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(scene => scene.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ───────────── 共通 ─────────────

    internal static void SetReference(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>箱を作る。useLocal が true のときは position を親からの位置として扱う</summary>
    internal static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool useLocal = false)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        if (useLocal) box.transform.localPosition = position;
        else box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        box.isStatic = !useLocal;
        return box;
    }

    static void SetLayerRecursive(GameObject target, int layer)
    {
        foreach (var child in target.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
    }

    /// <summary>レイヤーがなければ、空いている場所に追加する</summary>
    internal static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(element.stringValue)) continue;
            element.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return i;
        }

        Debug.LogError($"レイヤー「{layerName}」を追加できませんでした。空いているレイヤーがありません。");
        return 0;
    }

    internal static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        EnsureFolder(MaterialFolder);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
