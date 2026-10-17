using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// 模板一键引导：batchmode 执行
    ///   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod Dreamcore.EditorTools.Bootstrap.Run -logFile -
    /// 生成：URP 资产 + 梦核滤镜 Profile + 主场景（相机/Volume/UI 全层级）+ SpotsCatalog（扫描 Content/Spots 目录）+ 中文字体 + PlayerSettings。
    /// 幂等：重复执行覆盖生成。
    ///
    /// 目录约定（放图规则）：
    ///   Assets/Content/Spots/spot_01/normal.png        ← 正常画面（唯一）
    ///   Assets/Content/Spots/spot_01/anomaly_01.png     ← 异常变体（可多个，按文件名序）
    ///   支持 .png/.jpg/.jpeg；图片建议 960x540 或 16:9（ffmpeg 降质管线产物直接丢进来）。
    /// </summary>
    public static class Bootstrap
    {
        const string SettingsDir = "Assets/_Project/Settings";
        const string ScenePath = "Assets/_Project/Scenes/Main.unity";
        const string FontDir = "Assets/_Project/Fonts";
        const string FontPath = FontDir + "/simhei.ttf";
        const string CatalogPath = "Assets/Resources/SpotsCatalog.asset";
        const string ContentRel = "Content/Spots";

        public static void Run()
        {
            EnsureFolders();
            CreateUrpAssets();
            CopyFont();
            CreateCatalogFromSpots();
            CreateScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Bootstrap] 完成：URP + 滤镜 Profile + 场景 + UI + SpotsCatalog 已生成。");
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder(SettingsDir)) AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            if (!AssetDatabase.IsValidFolder(FontDir)) AssetDatabase.CreateFolder("Assets/_Project", "Fonts");
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Content")) AssetDatabase.CreateFolder("Assets", "Content");
            if (!AssetDatabase.IsValidFolder("Assets/Content/Spots")) AssetDatabase.CreateFolder("Assets/Content", "Spots");
        }

        static void CreateUrpAssets()
        {
            string rpPath = SettingsDir + "/URP-Dreamcore.asset";
            string rdPath = SettingsDir + "/URP-Dreamcore_Renderer.asset";

            // 成对重建（幂等）：无参 Create() 的 renderer data 只在内存里、存盘即丢引用，
            // 必须走官方路径——先建独立 renderer 资产，再传给 Create(rendererData)。
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath) != null)
                AssetDatabase.DeleteAsset(rpPath);
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath) != null)
                AssetDatabase.DeleteAsset(rdPath);

            var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rd, rdPath);
            var rp = UniversalRenderPipelineAsset.Create(rd);
            AssetDatabase.CreateAsset(rp, rpPath);

            GraphicsSettings.defaultRenderPipeline = rp;
            QualitySettings.renderPipeline = rp;
        }

        static void CopyFont()
        {
            if (AssetDatabase.LoadAssetAtPath<Font>(FontPath) != null) return;
            string src = @"C:\Windows\Fonts\simhei.ttf";
            if (!File.Exists(src))
            {
                Debug.LogWarning("[Bootstrap] 找不到系统 simhei.ttf，UI 将回退到默认字体（中文会缺字）。");
                return;
            }
            File.Copy(src, FontPath, true);
            AssetDatabase.ImportAsset(FontPath);
        }

        // ---------- 内容扫描 ----------

        static void CreateCatalogFromSpots()
        {
            string contentRoot = Path.Combine(Application.dataPath, ContentRel);
            var catalog = ScriptableObject.CreateInstance<SpotsCatalog>();
            if (Directory.Exists(contentRoot))
            {
                foreach (string d in Directory.GetDirectories(contentRoot))
                {
                    string dirName = Path.GetFileName(d);
                    if (!dirName.StartsWith("spot_")) continue;

                    string normalAbs = FindImage(d, "normal");
                    if (string.IsNullOrEmpty(normalAbs))
                    {
                        Debug.LogWarning("[Bootstrap] " + dirName + " 缺少 normal 图，跳过该机位。");
                        continue;
                    }

                    var spot = new Spot { id = dirName, displayName = dirName };
                    spot.normal = ImportAsSprite(ToAssetPath(normalAbs));
                    foreach (string f in Directory.GetFiles(d))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.StartsWith("anomaly_") && IsImage(fn))
                            spot.anomalies.Add(ImportAsSprite(ToAssetPath(f)));
                    }
                    spot.anomalies.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    catalog.spots.Add(spot);
                }
            }
            if (catalog.spots.Count == 0)
                Debug.LogWarning("[Bootstrap] Assets/Content/Spots 下没有 spot_XX 机位，Catalog 为空（玩法会提示）。");

            if (File.Exists(CatalogPath)) AssetDatabase.DeleteAsset(CatalogPath);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        static string FindImage(string dir, string baseName)
        {
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                string p = Path.Combine(dir, baseName + ext);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static bool IsImage(string fn)
        {
            string e = Path.GetExtension(fn).ToLowerInvariant();
            return e == ".png" || e == ".jpg" || e == ".jpeg";
        }

        static string ToAssetPath(string absolutePath)
        {
            return "Assets" + absolutePath.Substring(Application.dataPath.Length).Replace('\\', '/');
        }

        static AudioClip LoadAudio(string filename)
        {
            string p = "Assets/_Project/Audio/" + filename;
            return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        }

        static Sprite ImportAsSprite(string assetPath)
        {            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null)
            {
                AssetDatabase.ImportAsset(assetPath);
                imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            }
            if (imp != null &&
                (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled || imp.maxTextureSize > 1024))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        // ---------- 场景 ----------

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 相机（正交，只服务于 ScreenSpaceCamera UI + 全局后处理）
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.015f, 0.02f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask = ~0;

            // 全局 Volume（梦核滤镜）
            var volGo = new GameObject("GlobalVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = CreateDreamcoreProfile();

            // 玩法
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<GameManager>();
            gmGo.AddComponent<LoopController>();
            var audio = gmGo.AddComponent<AudioController>();
            audio.ambientLoop = LoadAudio("ambient_loop.wav");
            audio.footstep = LoadAudio("footstep.wav");
            audio.anomalyCue = LoadAudio("anomaly_cue.wav");

            // 事件系统（uGUI 按钮必需）
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            CreateUi(cam);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static VolumeProfile CreateDreamcoreProfile()
        {
            string path = SettingsDir + "/VolumeProfile-Dreamcore.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.8f);

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.28f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.42f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(new Color(0.08f, 0.06f, 0.05f));

            var adj = profile.Add<ColorAdjustments>(true);
            adj.saturation.Override(-30f);
            adj.contrast.Override(6f);
            adj.postExposure.Override(-0.25f);
            adj.colorFilter.Override(new Color(1.0f, 0.93f, 0.80f)); // 暖米黄

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.12f);

            var pan = profile.Add<PaniniProjection>(true);
            pan.distance.Override(0.015f);
            pan.cropToFit.Override(1f);

            return profile;
        }

        // ---------- UI ----------

        static void CreateUi(Camera cam)
        {
            var canvasGo = new GameObject("UI",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiController));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // 关键：UI 进相机渲染 → 后处理覆盖全部画面
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            // SpotImage（底层：当前机位画面）
            var spot = CreateUiObject("SpotImage", canvasGo.transform);
            var spotRaw = spot.AddComponent<RawImage>();
            spotRaw.color = Color.white;
            Stretch(spot.GetComponent<RectTransform>());

            // Scanlines（扫描线，不挡点击）
            var scan = CreateUiObject("Scanlines", canvasGo.transform);
            var scanRaw = scan.AddComponent<RawImage>();
            scanRaw.raycastTarget = false;
            Stretch(scan.GetComponent<RectTransform>());
            scan.AddComponent<ScanlineScroller>();

            // BlackOverlay（黑罩闪烁，不挡点击）
            var black = CreateUiObject("BlackOverlay", canvasGo.transform);
            var blackImg = black.AddComponent<Image>();
            blackImg.color = new Color(0f, 0f, 0f, 0f);
            blackImg.raycastTarget = false;
            Stretch(black.GetComponent<RectTransform>());
            black.AddComponent<FlickerController>();

            // HudPanel
            var hud = CreateUiObject("HudPanel", canvasGo.transform);
            Stretch(hud.GetComponent<RectTransform>());

            var depth = CreateText("DepthText", hud.transform, font, 44, new Vector2(0f, 0f), TextAnchor.MiddleLeft);
            AnchorTo(depth.rectTransform, new Vector2(0f, 0f));
            depth.rectTransform.anchoredPosition = new Vector2(90f, 150f);
            depth.color = new Color(0.88f, 0.86f, 0.78f);

            var mistakes = CreateText("MistakesText", hud.transform, font, 44, new Vector2(0f, 0f), TextAnchor.MiddleLeft);
            AnchorTo(mistakes.rectTransform, new Vector2(0f, 0f));
            mistakes.rectTransform.anchoredPosition = new Vector2(90f, 90f);
            mistakes.color = new Color(0.88f, 0.86f, 0.78f);

            var fwd = CreateButton("BtnForward", hud.transform, font, "前进 →", Vector2.zero, new Vector2(240f, 100f));
            AnchorTo(fwd.GetComponent<RectTransform>(), new Vector2(1f, 0f));
            fwd.GetComponent<RectTransform>().anchoredPosition = new Vector2(-160f, 120f);

            var back = CreateButton("BtnBack", hud.transform, font, "← 回头", Vector2.zero, new Vector2(240f, 100f));
            AnchorTo(back.GetComponent<RectTransform>(), new Vector2(0f, 0f));
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(160f, 120f);

            // TitlePanel
            var title = CreateUiObject("TitlePanel", canvasGo.transform);
            Stretch(title.GetComponent<RectTransform>());
            var titleBg = title.AddComponent<Image>();
            titleBg.color = new Color(0f, 0f, 0f, 0.85f);

            var titleText = CreateText("TitleText", title.transform, font, 100, new Vector2(0f, 180f), TextAnchor.MiddleCenter);
            titleText.text = "回 家";
            titleText.color = new Color(0.95f, 0.9f, 0.78f);

            var start = CreateButton("BtnStart", title.transform, font, "开 始", new Vector2(0f, -120f), new Vector2(340f, 120f));

            // EndingPanel
            var ending = CreateUiObject("EndingPanel", canvasGo.transform);
            Stretch(ending.GetComponent<RectTransform>());
            var endBg = ending.AddComponent<Image>();
            endBg.color = new Color(0f, 0f, 0f, 0.88f);

            var endingText = CreateText("EndingText", ending.transform, font, 56, new Vector2(0f, 100f), TextAnchor.MiddleCenter);
            endingText.color = new Color(0.95f, 0.9f, 0.78f);

            var restart = CreateButton("BtnRestart", ending.transform, font, "再 走 一 次", new Vector2(0f, -140f), new Vector2(380f, 120f));

            hud.SetActive(false);
            ending.SetActive(false);
        }

        static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AnchorTo(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
        }

        static Text CreateText(string name, Transform parent, Font font, int size, Vector2 pos, TextAnchor anchor)
        {
            var go = CreateUiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1700f, size * 1.6f);
            rt.anchoredPosition = pos;
            return text;
        }

        static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 pos, Vector2 size)
        {
            var go = CreateUiObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.05f, 0.55f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var txt = CreateText("Label", go.transform, font, 46, Vector2.zero, TextAnchor.MiddleCenter);
            txt.text = label;
            txt.raycastTarget = false;
            Stretch(txt.rectTransform);
            return btn;
        }

        // ---------- PlayerSettings ----------

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "Dreamcore";
            PlayerSettings.companyName = "dreamcore-jam";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }
    }
}
