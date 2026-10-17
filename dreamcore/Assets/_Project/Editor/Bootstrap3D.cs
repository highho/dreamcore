using System.Collections.Generic;
using Dreamcore.D3;
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
    /// 3D 轨道一键引导（本仓库唯一的出货轨道；2D 保底已于 2026-10-17 由用户裁定废弃并删除）：
    ///   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run
    /// 生成 Main3D.unity：第一人称 + 真实走廊 FBX（Corridor.fbx，MeshRenderer 涂占位材质并补碰撞体、
    ///   Light_ 锚点收养点光 + 灯闪、extraFigure 挂 billboard）+ 两端判定门 + 复用梦核滤镜 + HUD。
    ///
    /// 构建入口：-executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate3D → 后续 build 出 3D 版。
    ///
    /// 走廊实测世界尺寸（套 FbxYaw=90 后，logs/verify-fbx.log）：宽 2.91m × 高 3.10m × 长 24.05m，
    /// 玩家沿 +Z 行走（z 从 0 到 24.05）。CorridorWidth/Height/Length 三个常量保留作文档参考。
    /// </summary>
    public static class Bootstrap3D
    {
        const string Scene3DPath = "Assets/_Project/Scenes/Main3D.unity";
        const string ProfilePath = "Assets/_Project/Settings/VolumeProfile-Dreamcore.asset";
        const string FontPath = "Assets/_Project/Fonts/simhei.ttf";
        const string CorridorFbxPath = "Assets/Content/3D/Corridor.fbx";

        // FBX 根绕 Y 的旋转角度：走廊长度必须落在世界 +Z。唯一落点，装配与验收都读它。
        // 实测（Task 6 Step 6，logs/verify-fbx-yaw.log 用 -90f 跑）：-90f 下 size=(2.91,3.10,24.05)
        // 但 min.z=-24.05/max.z=0 —— 长度确实最长却朝 -Z 延伸（导入器先把 Blender X/Y 都翻了号），
        // 玩家一出门就撞端墙。按 brief 的分支表翻成 90f：实测 size.z=24.05、min.z=0/max.z=24.05。
        // 代价见 blender/README.md 轴映射小节：Blender +Y 那侧落到 Unity -X（左右与灰盒对调）。
        public const float FbxYaw = 90f;

        const float CorridorWidth = 2.4f;
        const float CorridorHeight = 3f;
        const float CorridorLength = 24f;

        public static void Run()
        {
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Bootstrap3D] 完成：Main3D.unity 已生成。用 Activate3D 切换构建入口。");
        }

        public static void Activate3D()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene3DPath, true) };
            Debug.Log("[Bootstrap3D] 构建入口已切到 3D（Main3D）。");
        }

        // ---------- 场景 ----------

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.10f, 0.09f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fogStartDistance = 2f;
            RenderSettings.fogEndDistance = 26f;

            var root = new GameObject("Corridor").transform;

            Material matFloor = MakeMat("Floor", new Color(0.30f, 0.28f, 0.25f));
            Material matWall = MakeMat("Wall", new Color(0.55f, 0.54f, 0.50f));
            Material matWainscot = MakeMat("Wainscot", new Color(0.22f, 0.38f, 0.26f)); // 绿漆墙裙
            Material matCeil = MakeMat("Ceiling", new Color(0.62f, 0.61f, 0.58f));
            Material matBlack = MakeUnlit("ShadeBlack", new Color(0.02f, 0.02f, 0.02f));

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CorridorFbxPath);
            if (fbx == null) Debug.LogError("[Bootstrap3D] 缺 " + CorridorFbxPath + "，先跑 ./tools/blender_build.sh");

            var inst = Object.Instantiate(fbx);
            inst.transform.SetParent(root, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, FbxYaw, 0f);

            // FBX 层级实测是扁平的（logs/verify-fbx.log：根 Corridor(Clone) 直接挂 44 个节点，
            // 父级是 EMPTY 的节点=0）：Blender 集合从不进 FBX（spec §9.1），异常语义只活在名字前缀里。
            // 所以 base/anom 的二分只能问 AnomalyResolver.IsAnomalyWireName（命名约定的唯一真相），
            // 既不去 FindDeep("base"/"anom")（那种节点不存在，会让下面三步整段静默不跑），
            // 也不在这里复制正则或自己写前缀判断。
            PaintShell(inst.transform, matFloor, matWall, matWainscot, matCeil);
            AdoptLights(inst.transform);
            AttachBillboards(inst.transform, matBlack);

            // 判定门：起点=回头，尽头=前进（走廊沿 +Z 从 0 到 24.05，见 R5 实测；x 一律 0=中线）
            MakeGate("GateBack", root, new Vector3(0f, 1.5f, 1.2f), false);
            MakeGate("GateForward", root, new Vector3(0f, 1.5f, CorridorLength - 1.2f), true);

            // Player：根(CharacterController) + 子相机
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.1f, 1.8f);
            player.AddComponent<CharacterController>(); // 场景内序列化：装配段要能读到，运行期 FPC.Awake 会 GetComponent 复用
            var fpc = player.AddComponent<FirstPersonController>();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask = ~0;
            fpc.cameraTransform = camGo.transform;

            // 全局 Volume（复用 2D 版梦核滤镜 Profile）
            var volGo = new GameObject("GlobalVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (vol.sharedProfile == null)
                Debug.LogWarning("[Bootstrap3D] 未找到 " + ProfilePath + "，先跑 2D 版 Bootstrap 生成滤镜 Profile。");

            // 玩法循环
            var loopGo = new GameObject("GameLoop");
            var loop = loopGo.AddComponent<Loop3D>();
            // FBX 前缀名 → 解析 → 钉成"正常"初态 → 交给 Loop3D（root 与实例化同一棵树）
            var entries = AnomalyResolver.Resolve(root);
            foreach (var e in entries) e.SettleToNormal();
            loop.anomalies = entries;
            if (entries.Count == 0) Debug.LogError("[Bootstrap3D] 异常表为空，玩法不成立。");

            CreateHud();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, Scene3DPath);
        }

        // ---------- FBX 收养 ----------

        /// <summary>
        /// 网格壳：涂占位材质 + 给 base 补碰撞体（FBX 两者都没有；不补就是穿墙走）。
        /// 含子层级——只遍历直接子节点这种写法在扁平层级下恰好够用，但递归更稳，
        /// Task 8/9 若引入嵌套容器不必回来改这里。
        /// base（非 wire 名）＝走廊本体，每个都要碰撞体；wire 名＝异常件（show_*），
        /// 涂材质但绝不补碰撞体，否则异常一出现就多出一批隐形墙。
        /// </summary>
        static void PaintShell(Transform shellRoot, Material floor, Material wall, Material wainscot, Material ceiling)
        {
            foreach (var mr in shellRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                if (AnomalyResolver.IsAnomalyWireName(n))
                {
                    mr.sharedMaterial = wall;   // 异常件占位色，正式贴图是 Task 8 的事
                    continue;                   // 关键：不给异常件补 Collider
                }
                if (n == "Floor") mr.sharedMaterial = floor;
                else if (n == "Ceiling") mr.sharedMaterial = ceiling;
                else if (n.StartsWith("Wainscot")) mr.sharedMaterial = wainscot;
                else mr.sharedMaterial = wall;
                if (mr.GetComponent<Collider>() == null) mr.gameObject.AddComponent<BoxCollider>();
            }
        }

        /// <summary>
        /// 灯位真相在 Blender（Light_2…Light_22 是 EMPTY），组件真相在 Unity。
        /// 组件必须直接 AddComponent 到锚点 GameObject 上：锚点自身带 100× 缩放
        /// （logs/verify-fbx.log：Light_2 lossyScale=(100,100,100)、world=(0,2.75,2)），
        /// 新建 GameObject 再 SetParent 到它下面会继承这个 100×，污染 localScale/localPosition。
        /// wire 名一律以 anom 开头，天然不会撞 Light_ 前缀，不需要额外排除条件。
        /// </summary>
        static void AdoptLights(Transform shellRoot)
        {
            foreach (var t in shellRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Light_")) continue;
                var l = t.GetComponent<Light>();
                if (l == null) l = t.gameObject.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.85f, 0.60f);
                l.intensity = 1.8f;
                l.range = 7f;
                l.shadows = LightShadows.None;
                if (t.GetComponent<LightFlicker>() == null) t.gameObject.AddComponent<LightFlicker>();
            }
        }

        /// <summary>人影要始终对着玩家才有效——billboard 组件只能由 Unity 挂，FBX 给不了。</summary>
        static void AttachBillboards(Transform shellRoot, Material black)
        {
            var fig = FindDeep(shellRoot, "anom02_show_extraFigure");
            if (fig == null) { Debug.LogWarning("[Bootstrap3D] 没找到 anom02_show_extraFigure，anom02 不会有人影。"); return; }
            var mr = fig.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = black;
            if (fig.GetComponent<ShadeFigure>() == null) fig.gameObject.AddComponent<ShadeFigure>();
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static void CreateHud()
        {
            var canvasGo = new GameObject("Hud3D", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Hud3D));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            var depth = MakeText("DepthText", canvasGo.transform, font, 40, new Vector2(90f, -120f), TextAnchor.UpperLeft);
            depth.color = new Color(0.88f, 0.86f, 0.78f);
            AnchorTopLeft(depth.rectTransform, new Vector2(90f, -120f));
            var mistakes = MakeText("MistakesText", canvasGo.transform, font, 40, new Vector2(90f, -170f), TextAnchor.UpperLeft);
            mistakes.color = new Color(0.88f, 0.86f, 0.78f);
            AnchorTopLeft(mistakes.rectTransform, new Vector2(90f, -170f));

            var end = MakeText("EndText", canvasGo.transform, font, 56, new Vector2(0f, 0f), TextAnchor.MiddleCenter);
            end.color = new Color(0.95f, 0.9f, 0.78f);
            end.gameObject.SetActive(false);

            var hud = canvasGo.GetComponent<Hud3D>();
            hud.depthText = depth;
            hud.mistakesText = mistakes;
            hud.endText = end;
        }

        // ---------- helpers ----------

        static Material MakeMat(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh);
            m.name = name;
            m.color = c;
            return m;
        }

        static Material MakeUnlit(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh);
            m.name = name;
            m.color = c;
            return m;
        }

        static GameObject MakeBox(string name, Transform parent, Vector3 center, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void MakeGate(string name, Transform parent, Vector3 center, bool isForward)
        {
            var go = MakeBox(name, parent, center, new Vector3(2f, 3f, 0.4f), null);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            var bc = go.GetComponent<BoxCollider>();
            bc.isTrigger = true;
            go.AddComponent<TriggerGate>().isForwardGate = isForward;
        }

        static void AnchorTopLeft(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
        }

        static Text MakeText(string name, Transform parent, Font font, int size, Vector2 anchoredPos, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(1500f, size * 1.6f);
            return t;
        }
    }
}
