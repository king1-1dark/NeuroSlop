using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NeuroSlop.EditorTools
{
    /// <summary>
    /// One-click, repeatable setup of the whole vertical slice: layers, generated art, data
    /// assets, prefabs, the three scenes and the build list. Menu: NeuroSlop/Build MVP.
    /// Re-running regenerates art/prefabs/scenes; tuning assets (configs, texts) are kept.
    /// </summary>
    public static class MvpBuilder
    {
        const string Root = "Assets/NeuroSlop/";
        const string PrefabDir = Root + "Prefabs/";
        const string DataDir = Root + "Data/";
        const string SceneDir = Root + "Scenes/";
        public const string MenuScene = SceneDir + "MainMenu.unity";
        public const string LevelScene = SceneDir + "Level_01.unity";
        public const string ResultsScene = SceneDir + "Results.unity";

        const float LevelEnd = 91f;
        const int UILayer = 5;

        static ArtGenerator.Result art;
        static Material spriteMaterial;
        static PhysicsMaterial2D noFriction;
        static VolumeProfile volumeProfile;
        static Font font;
        static NarrativeTexts texts;
        static EnemyConfig basicConfig;
        static EnemyConfig fastConfig;

        static readonly Color Yellow = new(1f, 0.84f, 0.2f);
        static readonly Color Green = new(0.45f, 1f, 0.55f);
        static readonly Color Pink = new(1f, 0.45f, 0.85f);
        static readonly Color Grey = new(0.65f, 0.65f, 0.72f);

        class Prefabs
        {
            public GameObject Player;
            public EnemyController Basic;
            public EnemyController Fast;
            public TimedEffect Spark;
            public FallingAsset Block;
            public SpriteRenderer Marker;
            public SpriteRenderer Portal;
        }

        [MenuItem("NeuroSlop/Build MVP (art, prefabs, scenes)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("NeuroSlop: exit Play Mode before building.");
                return;
            }

            EnsureLayers();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            art = ArtGenerator.GenerateAll();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateSharedAssets();
            var prefabs = CreatePrefabs();
            BuildMenuScene();
            BuildLevelScene(prefabs);
            BuildResultsScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScene, true),
                new EditorBuildSettingsScene(LevelScene, true),
                new EditorBuildSettingsScene(ResultsScene, true)
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuScene);
            Debug.Log("NeuroSlop: MVP built — scenes MainMenu / Level_01 / Results are in the build list.");
        }

        // ======================================================================== project

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in new[] { "Ground", "Player", "Enemy" })
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) exists = true;
                if (exists) continue;
                for (int i = 6; i < layers.arraySize; i++)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(p.stringValue)) continue;
                    p.stringValue = name;
                    break;
                }
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateSharedAssets()
        {
            spriteMaterial = LoadOrCreate(Root + "Art/M_SpriteGlitch.mat", () => new Material(Shader.Find("NeuroSlop/SpriteGlitch")));
            noFriction = LoadOrCreate(DataDir + "NoFriction.physicsMaterial2D", () => new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f });
            LoadOrCreate(Root + "Resources/GenerationCostConfig.asset", ScriptableObject.CreateInstance<GenerationCostConfig>);
            texts = LoadOrCreate(Root + "Resources/NarrativeTexts.asset", ScriptableObject.CreateInstance<NarrativeTexts>);

            basicConfig = LoadOrCreate(DataDir + "Enemy_Basic.asset", () =>
            {
                var c = ScriptableObject.CreateInstance<EnemyConfig>();
                c.displayName = "THUG_BOT.png";
                c.prompt = "robot thug, chrome, red visor, 16-bit, (menacing:1.2), cheap";
                c.maxHealth = 36; c.moveSpeed = 2.2f; c.detectRange = 14f;
                c.attackRange = 1.6f; c.windup = 0.55f; c.active = 0.12f; c.recovery = 0.6f; c.cooldown = 1.1f;
                c.damage = 12; c.hitOffset = new Vector2(0.95f, 1.4f); c.hitSize = new Vector2(1.3f, 1.0f); c.knockback = 5f;
                c.knockbackResistance = 0.2f;
                return c;
            });
            fastConfig = LoadOrCreate(DataDir + "Enemy_Fast.asset", () =>
            {
                var c = ScriptableObject.CreateInstance<EnemyConfig>();
                c.displayName = "SPRINT_BOT.png";
                c.prompt = "fast robot, neon magenta, blade arms, dynamic pose, low quality";
                c.maxHealth = 18; c.moveSpeed = 5f; c.detectRange = 16f;
                c.attackRange = 2.8f; c.windup = 0.3f; c.active = 0.2f; c.recovery = 0.3f; c.cooldown = 0.9f;
                c.damage = 7; c.hitOffset = new Vector2(0.8f, 1.1f); c.hitSize = new Vector2(1.2f, 0.9f); c.knockback = 3.5f;
                c.lungeSpeed = 9f; c.retreatTime = 0.7f;
                return c;
            });

            volumeProfile = LoadOrCreate(DataDir + "CorruptionVolume.asset", ScriptableObject.CreateInstance<VolumeProfile>);
            AddOverride<Bloom>(b => { b.intensity.Override(0.7f); b.threshold.Override(0.8f); });
            AddOverride<ChromaticAberration>(c => c.intensity.Override(0.1f));
            AddOverride<FilmGrain>(g => { g.type.Override(FilmGrainLookup.Medium3); g.intensity.Override(0.2f); });
            AddOverride<LensDistortion>(l => l.intensity.Override(0f));
            AddOverride<Vignette>(v => v.intensity.Override(0.32f));
        }

        static void AddOverride<T>(Action<T> setup) where T : VolumeComponent
        {
            if (volumeProfile.TryGet<T>(out _)) return;
            var component = volumeProfile.Add<T>(true);
            setup(component);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, volumeProfile);
            EditorUtility.SetDirty(volumeProfile);
        }

        static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Assigns a (private) serialized field — keeps runtime classes free of setup-only API.</summary>
        static void Set(Object target, string field, object value)
        {
            FieldInfo info = null;
            for (var t = target.GetType(); t != null && info == null; t = t.BaseType)
                info = t.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new MissingFieldException(target.GetType().Name, field);
            info.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }

        static int Layer(string name) => LayerMask.NameToLayer(name);

        // ======================================================================== prefabs

        static Prefabs CreatePrefabs()
        {
            var p = new Prefabs();

            var spark = new GameObject("HitSpark");
            AddSprite(spark, art["spark"], 30);
            spark.AddComponent<TimedEffect>();
            p.Spark = SavePrefab(spark).GetComponent<TimedEffect>();

            var marker = new GameObject("DropMarker");
            AddSprite(marker, art["marker"], 2);
            marker.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
            p.Marker = SavePrefab(marker).GetComponent<SpriteRenderer>();

            var portal = new GameObject("SpawnPortal");
            var portalView = portal.AddComponent<CharacterView>();
            Set(portalView, "body", AddSprite(portal, art["portal"], 4));
            Set(portalView, "idle", art["portal"]);
            Set(portalView, "baseGlitch", 0.45f);
            Set(portalView, "bobPixels", 0f);
            var progress = Child("Progress", portal.transform, new Vector3(-0.75f, 2.35f, 0f));
            AddSprite(progress, art["progress"], 5, new Color(0.4f, 1f, 0.5f));
            progress.transform.localScale = new Vector3(0f, 1f, 1f);
            p.Portal = SavePrefab(portal).GetComponent<SpriteRenderer>();

            var block = new GameObject("FallingAsset");
            var falling = block.AddComponent<FallingAsset>();
            var blockVisual = Child("Visual", block.transform, Vector3.zero);
            var blockView = blockVisual.AddComponent<CharacterView>();
            Set(blockView, "body", AddSprite(blockVisual, art["missing_block"], 12));
            Set(blockView, "idle", art["missing_block"]);
            Set(blockView, "bobPixels", 0f);
            Set(falling, "view", blockView);
            p.Block = SavePrefab(block).GetComponent<FallingAsset>();

            // Player
            var player = Character("Player", Layer("Player"), art["hero_idle"], null, art["hero_punch"], new Vector2(0.9f, 2.5f), 1.3f, 10, out var heroView);
            var heroHealth = player.AddComponent<Health>();
            Set(heroHealth, "team", Team.Player);
            Set(heroHealth, "maxHealth", 100);
            Set(heroHealth, "displayName", texts.heroName);
            player.AddComponent<PlayerInputReader>();
            var controller = player.AddComponent<PlayerController>();
            Set(controller, "view", heroView);
            player.AddComponent<PlayerCombat>();
            Set(heroView, "glitchSensitivity", 0.6f);
            p.Player = SavePrefab(player);

            p.Basic = Enemy("Enemy_Basic", basicConfig, "thug", new Vector2(1.1f, 2.3f), 1.5f);
            p.Fast = Enemy("Enemy_Fast", fastConfig, "sprinter", new Vector2(0.9f, 1.9f), 1.2f);
            return p;
        }

        static EnemyController Enemy(string name, EnemyConfig config, string spritePrefix, Vector2 collider, float shadow)
        {
            var go = Character(name, Layer("Enemy"), art[spritePrefix + "_idle"], art[spritePrefix + "_windup"], art[spritePrefix + "_attack"], collider, shadow, 8, out var view);
            var health = go.AddComponent<Health>();
            Set(health, "team", Team.Enemy);
            var enemy = go.AddComponent<EnemyController>();
            Set(enemy, "config", config);
            Set(enemy, "view", view);
            return SavePrefab(go).GetComponent<EnemyController>();
        }

        static GameObject Character(string name, int layer, Sprite idle, Sprite windup, Sprite attack, Vector2 colliderSize, float shadowScale, int order, out CharacterView view)
        {
            var root = new GameObject(name) { layer = layer };
            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 4f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = root.AddComponent<CapsuleCollider2D>();
            col.size = colliderSize;
            col.offset = new Vector2(0f, colliderSize.y * 0.5f);
            col.sharedMaterial = noFriction;

            var visual = Child("Visual", root.transform, Vector3.zero);
            view = visual.AddComponent<CharacterView>();
            Set(view, "body", AddSprite(visual, idle, order));
            Set(view, "idle", idle);
            Set(view, "windup", windup);
            Set(view, "attack", attack);

            var shadow = Child("Shadow", root.transform, Vector3.zero);
            AddSprite(shadow, art["shadow"], 1, new Color(0f, 0f, 0f, 0.5f));
            var ground = shadow.AddComponent<GroundShadow>();
            Set(ground, "owner", root.transform);
            Set(ground, "baseScale", shadowScale);
            return root;
        }

        static GameObject SavePrefab(GameObject go)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + go.name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Child(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        static SpriteRenderer AddSprite(GameObject go, Sprite sprite, int order, Color? color = null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.sharedMaterial = spriteMaterial;
            if (color.HasValue) sr.color = color.Value;
            return sr;
        }

        static SpriteRenderer SpriteObject(string name, Transform parent, Sprite sprite, int order, Vector3 localPosition, Color? color = null)
            => AddSprite(Child(name, parent, localPosition), sprite, order, color);

        // ================================================================== shared scene bits

        static Camera CreateCamera(bool pixelPerfect, Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(7.5f, 2.6f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 270f / 64f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 50f;
            go.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (pixelPerfect)
            {
                var ppc = go.AddComponent<PixelPerfectCamera>();
                ppc.assetsPPU = ArtGenerator.PPU;
                ppc.refResolutionX = 480;
                ppc.refResolutionY = 270;
                ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
                ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
            }
            return cam;
        }

        static GameObject CreateSystems(float corruptionBase)
        {
            var go = new GameObject("Systems");
            go.AddComponent<TimeControl>();
            Set(go.AddComponent<CorruptionSystem>(), "baseLevel", corruptionBase);
            return go;
        }

        static void CreateVolume()
        {
            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = volumeProfile;
            go.AddComponent<CorruptionPostFX>();
            // No Light2D on purpose: every sprite uses the unlit SpriteGlitch shader.
        }

        // ============================================================================= UI

        static Transform CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)) { layer = UILayer };
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return go.transform;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Rect measured from the parent's top-left corner, y grows downwards.</summary>
        static RectTransform TopLeft(string name, Transform parent, float x, float y, float w, float h)
            => Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

        static RectTransform Stretch(string name, Transform parent, Vector2? min = null, Vector2? max = null)
        {
            var rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = min ?? Vector2.zero;
            rt.anchorMax = max ?? Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Img(RectTransform rt, Color color, Sprite sprite = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(RectTransform rt, string text, int size, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal, bool outline = true)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.9f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        static GlitchText Glitch(Text t, float sensitivity = 1f, params string[] tierVariants)
        {
            var g = t.gameObject.AddComponent<GlitchText>();
            Set(g, "sensitivity", sensitivity);
            if (tierVariants.Length > 0)
            {
                Set(g, "tierVariants", tierVariants);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            return g;
        }

        static UIBar Bar(RectTransform rt, Color fillColor)
        {
            Img(rt, new Color(0.05f, 0.02f, 0.08f, 0.95f));
            var track = Stretch("Track", rt);
            track.offsetMin = new Vector2(3f, 3f);
            track.offsetMax = new Vector2(-3f, -3f);
            Img(track, new Color(0.18f, 0.08f, 0.14f, 1f));
            var trail = Stretch("Trail", track);
            Img(trail, new Color(1f, 1f, 1f, 0.85f));
            var fill = Stretch("Fill", track);
            Img(fill, fillColor);
            var bar = rt.gameObject.AddComponent<UIBar>();
            Set(bar, "fill", fill);
            Set(bar, "trail", trail);
            return bar;
        }

        static void GlitchOverlay(Transform canvas) => Stretch("GlitchOverlay", canvas).gameObject.AddComponent<ScreenGlitchOverlay>();

        static void SaveScene(string path) => EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);

        // ====================================================================== main menu

        static void BuildMenuScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(false, new Color(0.05f, 0.03f, 0.09f));
            CreateSystems(0.2f);
            CreateVolume();

            var canvas = CreateCanvas("MenuCanvas");
            Img(Stretch("Backdrop", canvas), new Color(0.05f, 0.03f, 0.09f));
            Img(Stretch("Art", canvas, new Vector2(0.52f, 0f), new Vector2(1f, 1f)), Color.white, art["menu_art"]).preserveAspect = true;
            Img(Stretch("Shade", canvas, Vector2.zero, new Vector2(0.6f, 1f)), new Color(0f, 0f, 0f, 0.5f));

            var title = Txt(TopLeft("Title", canvas, 100, 80, 1100, 320), "ДЕШЁВЫЙ\nНЕЙРОСЛОП", 132, Yellow, TextAnchor.UpperLeft, FontStyle.Bold);
            var titleOutline = title.GetComponent<Outline>();
            titleOutline.effectColor = new Color(1f, 0f, 0.8f, 1f);
            titleOutline.effectDistance = new Vector2(6f, -6f);
            Glitch(title, 1.6f);
            Txt(TopLeft("Subtitle", canvas, 100, 410, 1000, 50), "2D beat'em up · 16-bit · 100% сгенерировано*", 38, Color.white, TextAnchor.MiddleLeft);
            Glitch(Txt(TopLeft("Footnote", canvas, 100, 462, 1000, 40), "* кроме разработчика. Наверное.", 26, Grey, TextAnchor.MiddleLeft), 1.5f);
            var prompt = Txt(TopLeft("Prompt", canvas, 100, 530, 950, 140), "", 28, Pink, TextAnchor.UpperLeft);
            var press = Txt(TopLeft("PressStart", canvas, 100, 690, 1050, 60), "НАЖМИТЕ ENTER / (A) / START, ЧТОБЫ НАЧАТЬ", 44, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Txt(TopLeft("Controls", canvas, 100, 780, 950, 200),
                "← → / A D / стик / крестовина — движение\nSpace / K / ↑ / (A) — прыжок\nJ / X / Enter / (X) — удар\nEsc / Start — пауза     Esc / Backspace здесь — выход",
                28, new Color(0.8f, 0.85f, 1f), TextAnchor.UpperLeft);
            Glitch(Txt(Rect("Footer", canvas, Vector2.zero, Vector2.zero, new Vector2(100f, 30f), new Vector2(1200f, 40f)),
                "© 2026 HELPER. Все права принадлежат агенту.   build 0.1-mvp", 22, Grey, TextAnchor.MiddleLeft));
            GlitchOverlay(canvas);

            var menu = new GameObject("MainMenu").AddComponent<MainMenuController>();
            Set(menu, "promptLine", prompt);
            Set(menu, "pressStart", press);
            SaveScene(MenuScene);
        }

        // ========================================================================== level

        static void BuildLevelScene(Prefabs p)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = CreateCamera(true, new Color(0.07f, 0.05f, 0.12f));
            var rig = cam.gameObject.AddComponent<CameraRig>();
            var systems = CreateSystems(0.1f);
            var level = systems.AddComponent<LevelController>();
            Set(systems.AddComponent<HitFeedback>(), "sparkPrefab", p.Spark);
            systems.AddComponent<MetaDirector>();
            CreateVolume();

            // ---- environment
            var env = new GameObject("Environment").transform;
            var bg = SpriteObject("Background", env, art["bg_street"], -100, new Vector3(0f, 3.9f, 5f));
            bg.drawMode = SpriteDrawMode.Tiled;
            bg.size = new Vector2(140f, 11f);
            var bgParallax = bg.gameObject.AddComponent<ParallaxLayer>();
            Set(bgParallax, "factor", 0.35f);
            Set(bgParallax, "cameraTransform", cam.transform);

            var signs = Child("Signs", env, Vector3.zero).transform;
            var signsParallax = signs.gameObject.AddComponent<ParallaxLayer>();
            Set(signsParallax, "factor", 0.5f);
            Set(signsParallax, "cameraTransform", cam.transform);
            string[] signKeys = { "sign_nosleep", "sign_insertcoin", "sign_gameover" };
            float[] signX = { 5f, 12f, 19f, 26f, 33f, 40f };
            float[] signY = { 5.2f, 4.6f, 5.4f, 4.8f, 5.0f, 4.4f };
            for (int i = 0; i < signX.Length; i++)
                SpriteObject("Sign" + i, signs, art[signKeys[i % signKeys.Length]], -60, new Vector3(signX[i], signY[i], 4f));

            var ground = SpriteObject("Ground", env, art["ground"], -10, new Vector3(LevelEnd * 0.5f, -0.5f, 0f));
            ground.drawMode = SpriteDrawMode.Tiled;
            ground.size = new Vector2(LevelEnd + 20f, 1f);
            SpriteObject("Underground", env, art["pixel"], -11, new Vector3(LevelEnd * 0.5f, -3f, 0f), new Color(0.06f, 0.05f, 0.09f)).transform.localScale = new Vector3(LevelEnd + 20f, 4f, 1f);
            var groundCollider = Child("GroundCollider", env, new Vector3(LevelEnd * 0.5f, -2f, 0f));
            groundCollider.layer = Layer("Ground");
            groundCollider.AddComponent<BoxCollider2D>().size = new Vector2(LevelEnd + 30f, 4f);

            foreach (float x in new[] { 18f, 36f, 54f, 72f })
            {
                var gutter = SpriteObject("PanelGutter", env, art["gutter"], -40, new Vector3(x, 4f, 3f));
                gutter.drawMode = SpriteDrawMode.Tiled;
                gutter.size = new Vector2(0.5f, 12f);
            }
            Platform(env, 44f);
            Platform(env, 63f);

            var leftWall = Wall("LeftWall", env);
            var rightWall = Wall("RightWall", env);

            // ---- player & camera
            var player = (GameObject)PrefabUtility.InstantiatePrefab(p.Player);
            player.transform.position = new Vector3(3f, 0.05f, 0f);
            Set(rig, "target", player.transform);
            Set(rig, "levelMaxX", LevelEnd);
            Set(rig, "leftWall", leftWall);
            Set(rig, "rightWall", rightWall);

            // ---- encounters (comic panels)
            var encounters = new GameObject("Encounters").transform;
            var e1 = Encounter("Panel_02", encounters, 18f, 36f, 0.04f,
                "Генерирую противников. Промпт: «враги, много, дёшево».",
                Wave(Spawn(p.Basic, 14f, 0.3f)),
                Wave(Spawn(p.Basic, 3f, 0.2f), Spawn(p.Basic, 15f, 1.0f)));
            var e2 = Encounter("Panel_03", encounters, 36f, 54f, 0.05f,
                "Добавлен быстрый враг. Анимацию ходьбы сгенерировать не удалось.",
                Wave(Spawn(p.Fast, 15f, 0.3f)),
                Wave(Spawn(p.Basic, 15f, 0.2f), Spawn(p.Fast, 2f, 0.8f)));
            var e3 = Encounter("Panel_04", encounters, 54f, 72f, 0.06f,
                "Качество снижено для экономии токенов. Вы не заметите.",
                Wave(Spawn(p.Basic, 15f, 0.2f), Spawn(p.Fast, 13f, 0.6f)),
                Wave(Spawn(p.Fast, 2f, 0.3f), Spawn(p.Basic, 16f, 0.5f), Spawn(p.Fast, 14f, 0.9f)));
            var bossZone = Encounter("Panel_05_Boss", encounters, 72f, LevelEnd, 0.05f, "");
            Set(bossZone, "triggerOffset", 3f);
            Set(bossZone, "boss", BuildBoss(bossZone.transform, p));

            Set(level, "player", player.GetComponent<PlayerController>());
            Set(level, "cameraRig", rig);
            Set(level, "encounters", new[] { e1, e2, e3, bossZone });

            BuildHud();
            SaveScene(LevelScene);
        }

        static void Platform(Transform parent, float x)
        {
            var sr = SpriteObject("Dumpster", parent, art["dumpster"], -5, new Vector3(x, 0f, 1f));
            sr.gameObject.layer = Layer("Ground");
            var box = sr.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2.1f, 1.25f);
            box.offset = new Vector2(0f, 0.62f);
            box.usedByEffector = true;
            var effector = sr.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            effector.useSideFriction = false;
        }

        static Transform Wall(string name, Transform parent)
        {
            var go = Child(name, parent, new Vector3(0f, 5f, 0f));
            go.layer = Layer("Ground");
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 30f);
            return go.transform;
        }

        static EncounterZone.Spawn Spawn(EnemyController prefab, float x, float delay) => new() { prefab = prefab, x = x, delay = delay };
        static EncounterZone.Wave Wave(params EncounterZone.Spawn[] spawns) => new() { spawns = spawns };

        static EncounterZone Encounter(string name, Transform parent, float minX, float maxX, float corruption, string line, params EncounterZone.Wave[] waves)
        {
            var zone = Child(name, parent, new Vector3(minX, 0f, 0f)).AddComponent<EncounterZone>();
            Set(zone, "minX", minX);
            Set(zone, "maxX", maxX);
            Set(zone, "waves", waves);
            Set(zone, "corruptionOnEnter", corruption);
            Set(zone, "enterLine", line);
            return zone;
        }

        static BossController BuildBoss(Transform parent, Prefabs p)
        {
            var root = new GameObject("Boss_GENERATOR") { layer = Layer("Enemy") };
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(88.5f, 0f, 0.5f);
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(3.3f, 4.6f);
            box.offset = new Vector2(0f, 2.3f);
            Set(root.AddComponent<Health>(), "team", Team.Enemy);
            var boss = root.AddComponent<BossController>();

            var visual = Child("Visual", root.transform, Vector3.zero);
            var view = visual.AddComponent<CharacterView>();
            Set(view, "body", AddSprite(visual, art["boss_body"], 5));
            Set(view, "idle", art["boss_body"]);
            Set(view, "bobPixels", 0f);
            Set(view, "glitchSensitivity", 1.2f);
            var face = SpriteObject("Face", visual.transform, art["boss_face_idle"], 6, new Vector3(0f, 116f / 32f, -0.01f));
            var muzzle = Child("Muzzle", root.transform, new Vector3(-1.9f, 3.6f, 0f));

            var attacks = Child("Attacks", root.transform, Vector3.zero);
            var warning = SpriteObject("BeamWarning", attacks.transform, art["pixel"], 3, Vector3.zero, new Color(1f, 0.1f, 0.1f, 0.35f));
            var beamSprite = SpriteObject("Beam", attacks.transform, art["beam"], 20, Vector3.zero);
            warning.enabled = beamSprite.enabled = false;

            var beam = attacks.AddComponent<BeamAttack>();
            Set(beam, "warningBand", warning);
            Set(beam, "beam", beamSprite);
            Set(beam, "consoleLine", "render(beam) — луч по полу, прыгайте");
            var rain = attacks.AddComponent<AssetRainAttack>();
            Set(rain, "blockPrefab", p.Block);
            Set(rain, "markerPrefab", p.Marker);
            Set(rain, "consoleLine", "upload(assets) — уходите с красных меток");
            var spawn = attacks.AddComponent<SpawnMinionsAttack>();
            Set(spawn, "prefabs", new[] { p.Basic, p.Fast });
            Set(spawn, "portalPrefab", p.Portal);
            Set(spawn, "consoleLine", "generate(enemy) ×2");

            Set(boss, "phases", new[]
            {
                new BossPhase { name = "INFERENCE", enterAtHealth = 1f, attacks = new BossAttack[] { beam, rain, spawn }, idleTime = 1.4f, speed = 1f, corruptionOnEnter = 0.12f },
                new BossPhase
                {
                    name = "OVERFIT", enterAtHealth = 0.5f, attacks = new BossAttack[] { rain, beam, spawn, beam }, idleTime = 0.9f, speed = 1.3f,
                    corruptionOnEnter = 0.2f, bodyGlitch = 0.12f,
                    entryLines = new[] { "Перегенерация… перегенерация… перегенерация…", "Вы знаете, сколько стоит одна моя мысль?" }
                }
            });
            Set(boss, "view", view);
            Set(boss, "faceRenderer", face);
            Set(boss, "faceIdle", art["boss_face_idle"]);
            Set(boss, "faceCharge", art["boss_face_charge"]);
            Set(boss, "faceHurt", art["boss_face_hurt"]);
            Set(boss, "muzzle", muzzle.transform);
            return boss;
        }

        static void BuildHud()
        {
            var canvas = CreateCanvas("HUD");
            var hud = canvas.gameObject.AddComponent<HUDController>();

            // player
            var playerPanel = TopLeft("PlayerPanel", canvas, 30, 30, 640, 150);
            Img(playerPanel, new Color(0f, 0f, 0f, 0.6f));
            playerPanel.gameObject.AddComponent<UIDegrader>();
            var frame = TopLeft("PortraitFrame", playerPanel, 10, 10, 130, 130);
            Img(frame, Yellow);
            Img(TopLeft("Portrait", frame, 4, 4, 122, 122), Color.white, art["hero_portrait"]);
            var playerName = Glitch(Txt(TopLeft("Name", playerPanel, 156, 6, 470, 46), texts.heroName, 36, Yellow, TextAnchor.MiddleLeft, FontStyle.Bold));
            Glitch(Txt(TopLeft("HPLabel", playerPanel, 156, 60, 70, 36), "HP", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold), 1f, "HP", "HP", "HP?", "hp??");
            var playerBar = Bar(TopLeft("HPBar", playerPanel, 232, 62, 390, 32), Yellow);
            Glitch(Txt(TopLeft("Status", playerPanel, 156, 104, 470, 30), "статус: человек", 22, Grey, TextAnchor.MiddleLeft), 1.3f,
                "статус: человек", "статус: человек (не подтверждено)", "статус: ассет", "статус: ░░░░░");

            // last hit enemy
            var targetPanel = TopLeft("TargetPanel", canvas, 30, 190, 640, 70);
            Img(targetPanel, new Color(0f, 0f, 0f, 0.5f));
            var targetName = Glitch(Txt(TopLeft("Name", targetPanel, 12, 4, 616, 30), "ENEMY", 24, Pink, TextAnchor.MiddleLeft));
            var targetBar = Bar(TopLeft("Bar", targetPanel, 12, 38, 616, 22), new Color(1f, 0.3f, 0.75f));

            // usage monitor
            var usage = Rect("UsagePanel", canvas, Vector2.one, Vector2.one, new Vector2(-30f, -30f), new Vector2(540f, 220f));
            Img(usage, new Color(0f, 0.06f, 0.03f, 0.72f));
            usage.gameObject.AddComponent<UIDegrader>();
            Glitch(Txt(TopLeft("Header", usage, 16, 8, 510, 32), "HELPER · usage monitor", 22, Green, TextAnchor.MiddleLeft), 1f,
                "HELPER · usage monitor", "HELPER · usage monitor", "HELPER · usage (estimated)", "HELPER · usage ??? не смотрите");
            string[][] rows =
            {
                new[] { "ГЕНЕРАЦИИ", "ГЕНЕРАЦИИ", "ГЕНЕРАЦИИ", "ГЕНЕРАЦИИ (и ещё)" },
                new[] { "ЭНЕРГИЯ", "ЭНЕРГИЯ", "ЭНЕРГИЯ (оценка)", "ЭНЕРГИЯ (не смотрите)" },
                new[] { "ВОДА", "ВОДА", "ВОДА (охлаждение)", "ВОДА ░░░" },
                new[] { "ВРЕМЯ", "ВРЕМЯ", "ВРЕМЯ", "ВРЕМЯ?" }
            };
            var values = new GlitchText[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 48 + i * 42;
                Glitch(Txt(TopLeft("Label" + i, usage, 16, y, 250, 38), rows[i][0], 26, new Color(0.85f, 0.9f, 0.85f), TextAnchor.MiddleLeft), 1f, rows[i]);
                values[i] = Glitch(Txt(TopLeft("Value" + i, usage, 260, y, 264, 38), "0", 28, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleRight, FontStyle.Bold), 1.2f);
            }
            var popup = Txt(Rect("GenerationPopup", canvas, Vector2.one, Vector2.one, new Vector2(-30f, -256f), new Vector2(540f, 34f)), "", 24, Yellow, TextAnchor.MiddleRight);

            // agent console
            var console = Rect("AgentConsole", canvas, Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(1150f, 180f));
            Img(console, new Color(0f, 0f, 0f, 0.55f));
            console.gameObject.AddComponent<UIDegrader>();
            var lines = new Text[4];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = Txt(TopLeft("Line" + i, console, 16, 10 + i * 40, 1120, 38), "", 24, Green, TextAnchor.MiddleLeft);
            Set(console.gameObject.AddComponent<AgentConsole>(), "lines", lines);

            // boss
            // Top centre, between the player panel and the usage monitor: never covers the arena floor.
            var bossPanel = Rect("BossPanel", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(600f, 76f));
            var bossName = Glitch(Txt(TopLeft("Name", bossPanel, 0, 0, 600, 34), "GENERATOR", 28, new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold), 1.4f);
            var bossBar = Bar(TopLeft("Bar", bossPanel, 0, 40, 600, 28), new Color(0.95f, 0.15f, 0.2f));

            // messages
            var headline = Txt(Rect("Headline", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1700f, 130f)), "", 80, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            headline.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            var go = Txt(Rect("GoArrow", canvas, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 140f), new Vector2(360f, 110f)), "GO >>", 80, Yellow, TextAnchor.MiddleRight, FontStyle.Bold);
            var hint = Txt(Rect("ControlsHint", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1500f, 44f)), texts.controlsHint, 24, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);

            var prompts = Stretch("PromptFragments", canvas).gameObject.AddComponent<PromptFragmentLayer>();
            Set(prompts, "font", font);

            // game over
            var gameOver = Stretch("GameOverPanel", canvas);
            Img(gameOver, new Color(0f, 0f, 0f, 0.78f));
            Glitch(Txt(Rect("Title", gameOver, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1700f, 110f)),
                "DEVELOPER.EXE НЕ ОТВЕЧАЕТ", 76, new Color(1f, 0.3f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold), 1.5f);
            Txt(Rect("Prompt", gameOver, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1700f, 60f)),
                "Регенерировать разработчика?    Enter / J / (A)", 40, Color.white, TextAnchor.MiddleCenter);
            var cost = Txt(Rect("Cost", gameOver, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1700f, 50f)),
                "", 28, Grey, TextAnchor.MiddleCenter);

            // pause
            var pause = Stretch("PausePanel", canvas);
            Img(pause, new Color(0f, 0f, 0f, 0.6f));
            Txt(Rect("Title", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1600f, 110f)), "ПАУЗА", 90, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Txt(Rect("Keys", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1600f, 50f)),
                "Esc / Start — продолжить        Backspace / Select — выйти в меню", 32, Color.white, TextAnchor.MiddleCenter);
            Txt(Rect("Meta", pause, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1600f, 50f)),
                "Игра на паузе. Датацентр — нет: счётчик энергии продолжает идти.", 26, Green, TextAnchor.MiddleCenter);

            GlitchOverlay(canvas);

            Set(hud, "playerBar", playerBar);
            Set(hud, "playerName", playerName);
            Set(hud, "targetPanel", targetPanel.gameObject);
            Set(hud, "targetName", targetName);
            Set(hud, "targetBar", targetBar);
            Set(hud, "generations", values[0]);
            Set(hud, "energy", values[1]);
            Set(hud, "water", values[2]);
            Set(hud, "timer", values[3]);
            Set(hud, "generationPopup", popup);
            Set(hud, "bossPanel", bossPanel.gameObject);
            Set(hud, "bossName", bossName);
            Set(hud, "bossBar", bossBar);
            Set(hud, "headline", headline);
            Set(hud, "goArrow", go);
            Set(hud, "controlsHint", hint);
            Set(hud, "gameOverPanel", gameOver.gameObject);
            Set(hud, "gameOverCost", cost);
            Set(hud, "pausePanel", pause.gameObject);
        }

        // ======================================================================== results

        static void BuildResultsScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(false, new Color(0.03f, 0.03f, 0.05f));
            CreateSystems(0.35f);
            CreateVolume();

            var canvas = CreateCanvas("ResultsCanvas");
            Img(Stretch("Backdrop", canvas), new Color(0.03f, 0.03f, 0.06f));
            Img(Stretch("Art", canvas, Vector2.zero, new Vector2(0.37f, 1f)), Color.white, art["results_art"]).preserveAspect = true;

            var title = Txt(TopLeft("Title", canvas, 760, 60, 1120, 90), "ПРОХОЖДЕНИЕ ЗАВЕРШЕНО", 64, Yellow, TextAnchor.MiddleLeft, FontStyle.Bold);
            var labels = new Text[6];
            var values = new Text[6];
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 180 + i * 64;
                labels[i] = Txt(TopLeft("Label" + i, canvas, 760, y, 470, 56), "", 34, Color.white, TextAnchor.MiddleLeft);
                values[i] = Txt(TopLeft("Value" + i, canvas, 1230, y, 680, 56), "", 32, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
            }
            var comment = Txt(TopLeft("Comment", canvas, 760, 590, 1110, 400), "", 28, Green, TextAnchor.UpperLeft);
            var footer = Txt(Rect("Footer", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(1300f, 50f)),
                "Enter / (A) — сгенерировать заново        Esc / Backspace — в меню", 28, Color.white, TextAnchor.MiddleRight);
            GlitchOverlay(canvas);

            var results = new GameObject("Results").AddComponent<ResultsController>();
            Set(results, "title", title);
            Set(results, "labels", labels);
            Set(results, "values", values);
            Set(results, "comment", comment);
            Set(results, "footer", footer);
            SaveScene(ResultsScene);
        }
    }
}
