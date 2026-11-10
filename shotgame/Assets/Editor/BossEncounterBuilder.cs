using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BossEncounterBuilder
{
    public const string ScenePath = "Assets/Scenes/보스테스트.unity";
    public const string BossPrefabPath = "Assets/몬스터/챕터1보스몹/챕터1보스.prefab";
    public const string PatternPath = "Assets/SO/몬스터/챕터1보스패턴SO.asset";
    private const string FlyingMonsterPrefabPath = "Assets/몬스터/일반몹/플라잉몹_0.prefab";
    private const string ArtDirectory = "Assets/몬스터/챕터1보스몹";

    [MenuItem("Tools/Shotgame/보스/보스 프리팹과 테스트 씬 생성")]
    private static void BuildFromMenu()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
    }

    // Unity -batchmode -executeMethod BossEncounterBuilder.Build
    public static void Build()
    {
        Material lineMaterial = LoadOrCreateMaterial();
        Sprite tile = LoadOrCreateTile();
        MonsterSO bossData = LoadOrCreateMonsterData("챕터1보스SO", 120, 1);

        BossAttackSO patterns = AssetDatabase.LoadAssetAtPath<BossAttackSO>(PatternPath);
        if (patterns == null)
        {
            patterns = ScriptableObject.CreateInstance<BossAttackSO>();
            AssetDatabase.CreateAsset(patterns, PatternPath);
        }
        Set(patterns, "missilePrefab", Load<GameObject>("Assets/게임오브젝트/미사일탄환_0.prefab"));
        Set(patterns, "explosionPrefab", Load<GameObject>("Assets/게임오브젝트/미사일_16.prefab"));
        Set(patterns, "laserPrefab", Load<GameObject>("Assets/게임오브젝트/스나이퍼_0.prefab"));
        Set(patterns, "rangedMonsterPrefab", Load<GameObject>(FlyingMonsterPrefabPath));

        BuildBossPrefab(bossData, patterns, lineMaterial);
        GunSO gun = BuildDemoGun();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "보스테스트";
        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 0.5f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 5.8f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
        camera.gameObject.AddComponent<AudioListener>();
        Light2D light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        Transform stage = new GameObject("Boss Arena").transform;
        for (int i = 0; i < 11; i++)
            CreateBlock("격벽", stage, tile, lineMaterial, new Vector2(-10f + i * 2f, 0.5f),
                new Vector2(0.035f, 11f), new Color(0.075f, 0.12f, 0.17f), -20);
        for (int i = 0; i < 5; i++)
            CreateBlock("배경 가로선", stage, tile, lineMaterial, new Vector2(0f, -3f + i * 2f),
                new Vector2(23f, 0.025f), new Color(0.075f, 0.12f, 0.17f), -20);
        CreatePlatform("바닥", stage, tile, lineMaterial, new Vector2(0f, -4.35f), new Vector2(23f, 0.7f), false);
        CreatePlatform("왼쪽 발판", stage, tile, lineMaterial, new Vector2(-7f, -2.5f), new Vector2(2.6f, 0.35f), true);
        CreatePlatform("중간 발판", stage, tile, lineMaterial, new Vector2(-3.5f, -0.6f), new Vector2(2.6f, 0.35f), true);
        CreatePlatform("위쪽 발판", stage, tile, lineMaterial, new Vector2(0f, 1.3f), new Vector2(2.6f, 0.35f), true);
        CreatePlatform("왼쪽 벽", stage, tile, lineMaterial, new Vector2(-10.5f, 0f), new Vector2(0.5f, 12f), false);
        CreatePlatform("오른쪽 벽", stage, tile, lineMaterial, new Vector2(10.5f, 0f), new Vector2(0.5f, 12f), false);

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/캐릭터애니메이션/아리사/아리사 움직임_2.prefab"));
        player.name = "Arisa";
        player.transform.position = Vector3.zero;
        Physics2D.SyncTransforms();
        Bounds bounds = player.GetComponent<BoxCollider2D>().bounds;
        player.transform.position += new Vector3(-7.7f - bounds.center.x, -3.95f - bounds.min.y, 0f);
        Set(player.GetComponent<ArisaHealth>(), "maxHp", 8);
        Set(player.GetComponent<ArisaHealth>(), "currentHp", 8);
        Set(player.GetComponent<ArisaHorizontalMovement>(), "jumpSpeed", 9.5f);
        SubmachineGunFire fire = player.GetComponentInChildren<SubmachineGunFire>();
        Set(fire, "gunData", gun);
        Set(fire, "targetCamera", camera);
        ArisaMouseAim aim = player.GetComponent<ArisaMouseAim>();
        if (aim != null) Set(aim, "targetCamera", camera);

        GameObject boss = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BossPrefabPath));
        boss.transform.position = new Vector3(8f, -4f, 0f);
        Set(boss.GetComponent<BossMonsterController>(), "player", player.GetComponent<ArisaHealth>());

        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("BOSS_ENCOUNTER_BUILD_PASS: " + ScenePath);
    }

    private static void BuildBossPrefab(MonsterSO data, BossAttackSO patterns, Material lineMaterial)
    {
        GameObject boss = new GameObject("챕터1보스");
        try
        {
            boss.layer = LayerMask.NameToLayer("Enemy");
            GameObject visual = new GameObject("Boss Visual");
            visual.transform.SetParent(boss.transform, false);
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(ArtDirectory + "/보스몹1.png").OfType<Sprite>().First();
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;
            visual.transform.localPosition = new Vector3(-sprite.bounds.center.x, -sprite.bounds.min.y, 0f);
            BoxCollider2D collider = boss.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(sprite.bounds.size.x * 0.8f, sprite.bounds.size.y * 0.94f);
            collider.offset = new Vector2(0f, sprite.bounds.size.y * 0.5f);
            MonsterHealth health = boss.AddComponent<MonsterHealth>();
            Set(health, "monsterData", data);
            Transform muzzle = new GameObject("Laser Origin").transform;
            muzzle.SetParent(boss.transform, false);
            muzzle.localPosition = new Vector3(-1.4f, 0.5f, 0f);
            BossMonsterController controller = boss.AddComponent<BossMonsterController>();
            Set(controller, "attackData", patterns);
            Set(controller, "laserOrigin", muzzle);
            Set(controller, "lineMaterial", lineMaterial);
            PrefabUtility.SaveAsPrefabAsset(boss, BossPrefabPath);
        }
        finally { Object.DestroyImmediate(boss); }
    }

    private static MonsterSO LoadOrCreateMonsterData(string name, int hp, int damage)
    {
        string path = "Assets/SO/몬스터/" + name + ".asset";
        MonsterSO data = AssetDatabase.LoadAssetAtPath<MonsterSO>(path);
        if (data != null) return data;
        data = ScriptableObject.CreateInstance<MonsterSO>();
        AssetDatabase.CreateAsset(data, path);
        Set(data, "maxHp", hp);
        Set(data, "moveSpeed", 0f);
        Set(data, "attackDamage", damage);
        Set(data, "attackInterval", 1.8f);
        return data;
    }

    private static GunSO BuildDemoGun()
    {
        const string path = "Assets/SO/총/보스테스트기관단총SO.asset";
        GunSO gun = AssetDatabase.LoadAssetAtPath<GunSO>(path);
        if (gun != null) return gun;
        gun = Object.Instantiate(Load<GunSO>("Assets/SO/총/기관단총SO.asset"));
        gun.name = "보스테스트기관단총SO";
        Set(gun, "데미지", 1f);
        AssetDatabase.CreateAsset(gun, path);
        return gun;
    }

    private static Material LoadOrCreateMaterial()
    {
        string path = ArtDirectory + "/보스광선.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Sprite LoadOrCreateTile()
    {
        string path = ArtDirectory + "/보스아레나타일.png";
        if (!File.Exists(path))
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray());
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 2f;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        return Load<Sprite>(path);
    }

    private static GameObject CreateBlock(string name, Transform parent, Sprite tile, Material material,
        Vector2 position, Vector2 size, Color color, int order)
    {
        GameObject block = new GameObject(name);
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = tile;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = order;
        return block;
    }

    private static void CreatePlatform(string name, Transform parent, Sprite tile, Material material,
        Vector2 position, Vector2 size, bool oneWay)
    {
        GameObject block = CreateBlock(name, parent, tile, material, position, size,
            oneWay ? new Color(0.12f, 0.3f, 0.45f) : new Color(0.16f, 0.2f, 0.24f), 0);
        block.layer = LayerMask.NameToLayer(oneWay ? "Stair" : "Box");
        block.AddComponent<BoxCollider2D>();
        CreateBlock(name + " 상단", parent, tile, material, position + Vector2.up * (size.y * 0.5f),
            new Vector2(size.x, 0.045f), oneWay ? new Color(0.2f, 0.8f, 1f) : new Color(1f, 0.65f, 0.2f), 1);
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException("필수 에셋을 찾지 못했습니다: " + path);
        return asset;
    }

    private static void Set(Object target, string field, object value)
    {
        if (target == null) throw new InvalidOperationException("설정 대상 없음: " + field);
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + field + " 필드 없음");
        if (value is Object asset) property.objectReferenceValue = asset;
        else if (value is int integer) property.intValue = integer;
        else if (value is float number) property.floatValue = number;
        else throw new ArgumentException("지원하지 않는 설정: " + field);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
