using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Everything that is not terrain: asset import settings, FX materials, prop prefabs,
// the zombie animator, and wiring of the gameplay and menu scenes.
public static class GameplayBuilder
{
    const string Res = "Assets/Resources";
    const string PropsDir = "Assets/Environment/Generated/Props";
    const string ZombiePrefab = "Assets/Prefabs/Zombie1.prefab";
    const string ZombieController = "Assets/Animations/ZombieBrain.controller";
    const string KnightModel = "Assets/Toon_RTS_demo/models/ToonRTS_demo_Knight.FBX";
    const string KnightController = "Assets/Animations/KnightController.controller";
    public const string MenuScene = "Assets/Scenes/MainMenu.unity";

    // ------------------------------------------------------------ assets

    public static void SetupAssets()
    {
        ConfigureUiSprites();
        ConfigureCustomisationTextures();
        ConfigureAudio();
        Directory.CreateDirectory(PropsDir);
        Directory.CreateDirectory(Res + "/FX");
        Directory.CreateDirectory(Res + "/Prefabs");
        AssetDatabase.Refresh();
        CreateFxMaterials();
        CreatePropPrefabs();
        CreateZombieAnimator();
        ConfigureZombiePrefab();
        AssetDatabase.SaveAssets();
    }

    static void ConfigureUiSprites()
    {
        // 9-slice borders (left, bottom, right, top) in pixels
        var borders = new (string name, Vector4 border)[]
        {
            ("panel_dark", new Vector4(48, 48, 48, 48)), ("panel_iron", new Vector4(48, 48, 48, 48)), ("panel_parchment", new Vector4(40, 40, 40, 40)),
            ("button", new Vector4(30, 24, 30, 24)), ("button_hover", new Vector4(30, 24, 30, 24)), ("button_pressed", new Vector4(30, 24, 30, 24)), ("button_dark", new Vector4(30, 24, 30, 24)),
            ("bar_frame", new Vector4(40, 14, 40, 14)), ("bar_bg", new Vector4(6, 6, 6, 6)), ("slot", new Vector4(30, 30, 30, 30)), ("slot_active", new Vector4(30, 30, 30, 30)),
            ("checkbox", new Vector4(16, 16, 16, 16)), ("banner", new Vector4(110, 20, 110, 20)),
        };
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Res + "/UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            string name = Path.GetFileNameWithoutExtension(path);
            var border = borders.FirstOrDefault(b => b.name == name).border;
            bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != border || ti.mipmapEnabled || !ti.alphaIsTransparency;
            if (!changed) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spriteBorder = border;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }
    }

    static void ConfigureCustomisationTextures()
    {
        foreach (var name in new[] { "KnightBase", "KnightMask" })
        {
            string path = $"{Res}/Customization/{name}.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            bool mask = name == "KnightMask";
            if (ti.isReadable && ti.textureCompression == TextureImporterCompression.Uncompressed && ti.sRGBTexture == !mask) continue;
            ti.isReadable = true;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = !mask;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    static void ConfigureAudio()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Res + "/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ai = (AudioImporter)AssetImporter.GetAtPath(path);
            bool longClip = path.Contains("music") || path.Contains("ambient");
            var s = ai.defaultSampleSettings;
            var want = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            if (s.loadType == want && ai.forceToMono == !longClip) continue;
            s.loadType = want;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = longClip ? 0.6f : 0.75f;
            ai.defaultSampleSettings = s;
            ai.forceToMono = !longClip;
            ai.loadInBackground = longClip;
            ai.SaveAndReimport();
        }
    }

    static Material SaveMat(string path, Material m)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.CopyPropertiesFromMaterial(m); existing.shader = m.shader; existing.shaderKeywords = m.shaderKeywords; existing.renderQueue = m.renderQueue; EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void CreateFxMaterials()
    {
        var shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit");
        var particle = new Material(shader) { name = "Particle" };
        particle.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Res + "/UI/glow.png");
        if (particle.HasProperty("_TintColor")) particle.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        SaveMat(Res + "/FX/Particle.mat", particle);

        var acid = new Material(Shader.Find("Standard")) { name = "Acid" };
        acid.color = new Color(0.36f, 0.62f, 0.06f, 0.5f);
        acid.SetFloat("_Mode", 2f);
        acid.SetOverrideTag("RenderType", "Transparent");
        acid.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        acid.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        acid.SetInt("_ZWrite", 0);
        acid.DisableKeyword("_ALPHATEST_ON");
        acid.EnableKeyword("_ALPHABLEND_ON");
        acid.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        acid.EnableKeyword("_EMISSION");
        acid.SetColor("_EmissionColor", new Color(0.1f, 0.26f, 0.01f));
        acid.SetFloat("_Glossiness", 0.85f);
        acid.renderQueue = 3000;
        SaveMat(Res + "/FX/Acid.mat", acid);
    }

    static GameObject SaveProp(string name, Mesh mesh, Material[] runtimeMats, bool collider = false)
    {
        string meshPath = $"{PropsDir}/{name}_Mesh.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(Object.Instantiate(mesh), meshPath);
        var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        var mats = runtimeMats.Select(m => SaveMat($"{PropsDir}/{m.name}.mat", new Material(m) { name = m.name })).ToArray();

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = savedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        if (collider) go.AddComponent<BoxCollider>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Res}/Prefabs/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void CreatePropPrefabs()
    {
        SaveProp("Arrow", ProceduralModels.ArrowMesh(), ProceduralModels.ArrowMaterials());
        SaveProp("Bow", ProceduralModels.BowMesh(), ProceduralModels.BowMaterials(0));
        SaveProp("ArrowBundle", ProceduralModels.ArrowBundleMesh(), ProceduralModels.BundleMaterials());
        SaveProp("Potion", ProceduralModels.PotionMesh(), ProceduralModels.PotionMaterials());
        SaveProp("SupplyCrate", ProceduralModels.CrateMesh(), ProceduralModels.CrateMaterials(), true);
        // all bow finishes as assets too, so their shader variants ship in builds
        for (int i = 0; i < GameSettings.BowFinishes.Length; i++)
            foreach (var m in ProceduralModels.BowMaterials(i)) SaveMat($"{PropsDir}/{m.name}.mat", new Material(m) { name = m.name });
    }

    // ------------------------------------------------------------ zombie animation

    static AnimationClip Clip(string name, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Zombie/Animations/{name}.anim");
        if (clip == null) throw new Exception("Missing zombie clip " + name);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        if (s.loopTime != loop)
        {
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            EditorUtility.SetDirty(clip);
        }
        return clip;
    }

    static void CreateZombieAnimator()
    {
        AssetDatabase.DeleteAsset(ZombieController);
        var c = AnimatorController.CreateAnimatorControllerAtPath(ZombieController);
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter(new AnimatorControllerParameter { name = "MoveMul", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        c.AddParameter(new AnimatorControllerParameter { name = "AttackMul", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        c.AddParameter("attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("die", AnimatorControllerParameterType.Trigger);
        c.AddParameter("dieForward", AnimatorControllerParameterType.Bool);
        c.AddParameter("isMoving", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;

        // locomotion: in-place clips blended by real agent speed (no root motion = no sliding/fighting)
        var tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, c);
        tree.AddChild(Clip("Z_Idle", true), 0f);
        tree.AddChild(Clip("Z_Walk_InPlace", true), 1.1f);
        tree.AddChild(Clip("Z_Run_InPlace", true), 3.8f);
        var loco = sm.AddState("Locomotion");
        loco.motion = tree;
        loco.speedParameter = "MoveMul";
        loco.speedParameterActive = true;
        sm.defaultState = loco;

        var attack = sm.AddState("Attack");
        attack.motion = Clip("Z_Attack", false);
        attack.speedParameter = "AttackMul";
        attack.speedParameterActive = true;
        var toAttack = loco.AddTransition(attack);
        toAttack.AddCondition(AnimatorConditionMode.If, 0, "attack");
        toAttack.hasExitTime = false; toAttack.duration = 0.08f;
        var back = attack.AddTransition(loco);
        back.hasExitTime = true; back.exitTime = 0.92f; back.duration = 0.18f;

        var dieBack = sm.AddState("Die Back"); dieBack.motion = Clip("Z_FallingBack", false);
        var dieFwd = sm.AddState("Die Forward"); dieFwd.motion = Clip("Z_FallingForward", false);
        foreach (var (state, forward) in new[] { (dieBack, false), (dieFwd, true) })
        {
            var t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0, "die");
            t.AddCondition(forward ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "dieForward");
            t.hasExitTime = false; t.duration = 0.12f; t.canTransitionToSelf = false;
        }
        EditorUtility.SetDirty(c);
    }

    static void ConfigureZombiePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(ZombiePrefab);
        try
        {
            var anim = root.GetComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ZombieController);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = 0.38f;
            agent.height = 1.8f;
            agent.stoppingDistance = 0.2f;
            var cap = root.GetComponent<CapsuleCollider>();
            cap.radius = 0.32f;
            var ec = root.GetComponent<EnemyController>();
            ec.type = ZombieType.Walker;
            PrefabUtility.SaveAsPrefabAsset(root, ZombiePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // let MaterialPropertyBlock flashes/tints show up in emission
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Zombie/Materials" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m.shader.name != "Standard" || m.IsKeywordEnabled("_EMISSION")) continue;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
        }
    }

    public static GameObject CratePrefab => AssetDatabase.LoadAssetAtPath<GameObject>(Res + "/Prefabs/SupplyCrate.prefab");

    // ------------------------------------------------------------ gameplay scene

    public static void SetupGameScene(Scene game)
    {
        foreach (var root in game.GetRootGameObjects())
        {
            // the old hand-made HUD is replaced by GameHUD
            if (root.name == "Canvas" && root.GetComponent<Canvas>() != null) Object.DestroyImmediate(root);
        }
        var player = game.GetRootGameObjects().FirstOrDefault(g => g.CompareTag("Player"));
        if (player != null)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) { ph.maxHealth = 100; EditorUtility.SetDirty(ph); }
            var pc = player.GetComponent<PlayerControlSpeed>();
            if (pc != null) { pc.walkSpeed = 3.4f; pc.runSpeed = 6.8f; pc.blockSpeed = 1.8f; EditorUtility.SetDirty(pc); }
            if (player.GetComponent<KnightAppearance>() == null) player.AddComponent<KnightAppearance>();
            var obstacle = player.GetComponent<NavMeshObstacle>();
            if (obstacle != null) { obstacle.carving = false; EditorUtility.SetDirty(obstacle); } // carving a hole round the player made zombies path oddly
        }
        var cam = Camera.main != null && Camera.main.gameObject.scene == game ? Camera.main : game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).FirstOrDefault();
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow != null) { follow.smoothSpeed = 0.08f; follow.minPitch = -20f; follow.maxPitch = 65f; follow.maxZoom = 12f; EditorUtility.SetDirty(follow); }
        }
        EditorSceneManager.MarkSceneDirty(game);
    }

    // ------------------------------------------------------------ menu scene

    public static void SetupMenuScene(Vector2 castleCenter, float castleRadius, Material sky, Action<Scene, Material> applyLighting)
    {
        var menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        foreach (var r in menu.GetRootGameObjects().Where(g => g.name == "Menu Camera" || g.name == "Knight Preview" || g.name == "Sun" || g.name == "PostFX Volume"))
            Object.DestroyImmediate(r);
        // stale cameras/listeners from the old menu would fight the new ones
        foreach (var c in menu.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))) c.enabled = false;

        // sun (the world scene carries no directional light of its own)
        var sunGo = new GameObject("Sun");
        SceneManager.MoveGameObjectToScene(sunGo, menu);
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        applyLighting(menu, sky);

        // cinematic camera
        var camGo = new GameObject("Menu Camera") { tag = "MainCamera" };
        SceneManager.MoveGameObjectToScene(camGo, menu);
        var cam = camGo.AddComponent<Camera>();
        cam.farClipPlane = 1500f; cam.nearClipPlane = 0.3f; cam.fieldOfView = 50f; cam.allowHDR = true; cam.allowMSAA = false;
        camGo.AddComponent<AudioListener>();
        var orbit = camGo.AddComponent<MenuCameraOrbit>();
        orbit.center = new Vector3(castleCenter.x, 0, castleCenter.y);
        orbit.radius = castleRadius * 1.9f;
        orbit.height = castleRadius * 0.45f;
        foreach (var l in menu.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioListener>(true)))
            if (l.gameObject != camGo) l.enabled = false;

        var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>("Assets/Environment/Generated/PostFX.asset");
        if (profile != null)
        {
            var layer = camGo.AddComponent<PostProcessLayer>();
            var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            layer.Init(res);
            layer.volumeTrigger = camGo.transform;
            layer.volumeLayer = 1;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            var so = new SerializedObject(layer); so.FindProperty("m_Resources").objectReferenceValue = res; so.ApplyModifiedPropertiesWithoutUndo();
            var vol = new GameObject("PostFX Volume").AddComponent<PostProcessVolume>();
            SceneManager.MoveGameObjectToScene(vol.gameObject, menu);
            vol.isGlobal = true; vol.sharedProfile = profile;
        }

        // knight on a turntable, far below the world, rendered to a texture for the customise page
        var previewRoot = new GameObject("Knight Preview");
        SceneManager.MoveGameObjectToScene(previewRoot, menu);
        previewRoot.transform.position = new Vector3(0, -500, 0);
        var knightAsset = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModel);
        var knight = (GameObject)PrefabUtility.InstantiatePrefab(knightAsset, previewRoot.transform);
        knight.transform.localPosition = Vector3.zero;
        knight.transform.localRotation = Quaternion.Euler(0, 160, 0);
        if (!knight.TryGetComponent(out Animator anim)) anim = knight.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KnightController);
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        knight.AddComponent<KnightAppearance>();

        var plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinth.name = "Plinth";
        Object.DestroyImmediate(plinth.GetComponent<Collider>());
        plinth.transform.SetParent(previewRoot.transform, false);
        plinth.transform.localPosition = new Vector3(0, -0.06f, 0);
        plinth.transform.localScale = new Vector3(1.6f, 0.06f, 1.6f);
        plinth.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Environment/Generated/FoundationStone.mat");

        var pcamGo = new GameObject("Preview Camera");
        pcamGo.transform.SetParent(previewRoot.transform, false);
        pcamGo.transform.localPosition = new Vector3(0, 1.15f, 3.4f);
        pcamGo.transform.LookAt(previewRoot.transform.position + Vector3.up * 0.95f);
        var pcam = pcamGo.AddComponent<Camera>();
        pcam.fieldOfView = 34f; pcam.nearClipPlane = 0.1f; pcam.farClipPlane = 20f;
        pcam.clearFlags = CameraClearFlags.SolidColor; pcam.backgroundColor = new Color(0, 0, 0, 0);
        pcam.enabled = false;

        var rim = new GameObject("Rim Light").AddComponent<Light>();
        rim.transform.SetParent(previewRoot.transform, false);
        rim.transform.localPosition = new Vector3(-1.5f, 2.6f, -1.8f);
        rim.type = LightType.Point; rim.range = 6f; rim.intensity = 2.2f; rim.color = new Color(1f, 0.85f, 0.6f);
        var fill = new GameObject("Fill Light").AddComponent<Light>();
        fill.transform.SetParent(previewRoot.transform, false);
        fill.transform.localPosition = new Vector3(1.4f, 1.6f, 2.4f);
        fill.type = LightType.Point; fill.range = 6f; fill.intensity = 1.1f; fill.color = new Color(0.75f, 0.85f, 1f);

        var mp = previewRoot.AddComponent<MenuPreview>();
        mp.previewCamera = pcam;
        mp.knight = knight.transform;

        // make sure the menu controller exists
        var controller = Object.FindFirstObjectByType<MainController>();
        if (controller == null)
        {
            var go = new GameObject("MenuController");
            SceneManager.MoveGameObjectToScene(go, menu);
            go.AddComponent<MainController>();
        }

        EditorSceneManager.MarkSceneDirty(menu);
        EditorSceneManager.SaveScene(menu);
    }
}
