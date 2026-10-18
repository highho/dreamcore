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
    /// 生成 Main3D.unity：第一人称 + 真实走廊 FBX（Corridor.fbx，MeshRenderer 按名分派 ambientCG PBR
    ///   材质并按世界尺寸做 world-scale tiling、补碰撞体、Light_ 锚点收养点光 + 灯闪、extraFigure 挂 billboard）
    ///   + 两端判定门 + 复用梦核滤镜 + HUD（ScreenSpaceCamera）。
    ///
    /// 构建入口：-executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate3D → 后续 build 出 3D 版。
    ///
    /// 走廊实测世界尺寸（套 FbxYaw=90 后，logs/verify-asm.log 的 active base 聚合包围盒）：
    /// 宽 2.50m × 高 3.10m × 长 24.05m，玩家沿 +Z 行走（z 从 0 到 24.05）。
    /// 旧口径"宽 2.91m"作废：那是窗外楼板露在墙外约 0.5m 撑出来的假宽度，M3 把窗板收进墙体后为 2.50m。
    /// CorridorWidth/Height/Length 三个常量保留作文档参考。
    /// </summary>
    public static class Bootstrap3D
    {
        const string Scene3DPath = "Assets/_Project/Scenes/Main3D.unity";
        const string ProfilePath = "Assets/_Project/Settings/VolumeProfile-Dreamcore.asset";
        const string FontPath = "Assets/_Project/Fonts/simhei.ttf";
        const string CorridorFbxPath = "Assets/Content/3D/Corridor.fbx";
        const string BodyFbxPath = "Assets/Content/3D/Body.fbx";   // 腿脚单独一棵，理由见 blender/gen_body.py 头部
        const string TexRoot = "Assets/Content/Textures";   // 全 CC0：墙面/地面来自 ambientCG（fetch_textures.sh），Prop_* 来自 Poly Haven（fetch_props.sh）
        const string AudioRoot = "Assets/_Project/Audio";   // ffmpeg 纯合成，由 ./tools/make_audio.sh 落盘

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

            var mats = MakeMats();
            // tile 签名（材质名+取整后的 tile）相同的网格共用一份实例：走廊里 24m 长的大面只有
            // 四五块，逐网格 new 会把场景文件灌成几十份近重复材质。
            var tiledCache = new Dictionary<string, Material>();

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
            PaintShell(inst.transform, mats, tiledCache);
            AdoptLights(inst.transform);
            AttachBillboards(inst.transform, mats);

            // 判定门：起点=回头，尽头=前进（走廊沿 +Z 从 0 到 24.05，见 R5 实测；x 一律 0=中线）
            MakeGate("GateBack", root, new Vector3(0f, 1.5f, 1.2f), false);
            MakeGate("GateForward", root, new Vector3(0f, 1.5f, CorridorLength - 1.2f), true);

            // Player：根＝脚底(CharacterController) + 子相机 + 同级腿脚
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.1f, 1.8f);   // 0.1＝楼板顶 FLOOR_TOP，根就是脚底，不用再加半个身高
            var cc = player.AddComponent<CharacterController>(); // 场景内序列化：装配段要能读到，运行期 FPC.Awake 会 GetComponent 复用
            // center 抬到半高是让"根＝脚底"的关键。默认序列化值是 height 2 / center 0，
            // 那时根落在身体中点、相机再叠 1.6 ⇒ 眼睛离地 2.70m（logs/shots7 里"飘在走廊"的根因，
            // 天花板才 3.10m）。半径 0.5 不动：HUD 的 planeDistance=0.4 是按它推出来的（见 CreateHud）。
            cc.height = 1.8f;
            cc.radius = 0.5f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            var fpc = player.AddComponent<FirstPersonController>();
            fpc.footstepClip = LoadClip("footstep.wav");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            // y 1.65+0.1＝眼睛离地 1.75m（成年人视高）。z 退到 -0.30：腿柱原本正长在眼睛底下，
            // 低头到 pitchLimit 时视线先撞到大腿截面（0.7m 外、半径 0.115 ⇒ 垂角张角 ±9°），
            // 鞋被整条腿挡死，logs/shots9 的 08 帧里两条腿读起来就是"两个圆饼"。
            // 眼睛往后挪 0.3m 后，大腿垂角从 90° 降到 ~68°，鞋落到 ~77°，低头时两者错开、
            // 腿有前后层次。-0.30 仍在半径 0.5 的胶囊内，不会让相机穿到碰撞体外。
            camGo.transform.localPosition = new Vector3(0f, 1.65f, -0.30f);
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask = ~0;
            fpc.cameraTransform = camGo.transform;
            AssembleBody(player.transform, mats, fpc);

            // 全局 Volume。注意：VolumeProfile-Dreamcore.asset 出厂时 components 是空的，
            // "复用了 2D 版滤镜"这个说法在资产层面并不成立——滤镜得在这里真填进去。
            var volGo = new GameObject("GlobalVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = EnsureDreamcoreProfile();
            if (vol.sharedProfile == null)
                Debug.LogError("[Bootstrap3D] 没有 VolumeProfile 资产 " + ProfilePath + "，梦核滤镜整条链缺失。");

            // 环境底噪：60s 粉噪+50Hz 电流声，全局循环、不做 3D 定位（spatialBlend=0）。
            // 挂在场景根而非相机下：相机跟着玩家转，底噪跟着相机走会听出"声音粘在耳上"。
            var ambGo = new GameObject("AmbientAudio");
            var amb = ambGo.AddComponent<AudioSource>();
            amb.clip = LoadClip("ambient_loop.wav");
            amb.loop = true;
            amb.playOnAwake = true;
            amb.spatialBlend = 0f;
            amb.volume = 0.35f;

            // 玩法循环
            var loopGo = new GameObject("GameLoop");
            var loop = loopGo.AddComponent<Loop3D>();
            loop.anomalyCue = LoadClip("anomaly_cue.wav");
            loop.decisionClip = LoadClip("tape_click.wav");
            // FBX 前缀名 → 解析 → 钉成"正常"初态 → 交给 Loop3D（root 与实例化同一棵树）
            var entries = AnomalyResolver.Resolve(root);
            foreach (var e in entries) e.SettleToNormal();
            loop.anomalies = entries;
            if (entries.Count == 0) Debug.LogError("[Bootstrap3D] 异常表为空，玩法不成立。");

            CreateHud(cam);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, Scene3DPath);
        }

        // ---------- FBX 收养 ----------

        /// <summary>
        /// 网格壳：按名分派真实 PBR 材质 + 给 base 补碰撞体（FBX 两者都没有；不补就是穿墙走）。
        /// 含子层级——只遍历直接子节点这种写法在扁平层级下恰好够用，但递归更稳，
        /// Task 8/9 若引入嵌套容器不必回来改这里。
        /// base（非 wire 名）＝走廊本体，每个都要碰撞体；wire 名＝异常件（show_*），
        /// 涂材质但绝不补碰撞体，否则异常一出现就多出一批隐形墙。
        /// 异常件按**语义**分材质，不再统一涂墙色：anom10 的本体是"窗外该有楼的地方全黑"，
        /// 涂成墙色会把它渲染成"窗外变亮"——与这条异常要传达的东西正好相反。
        /// </summary>
        static void PaintShell(Transform shellRoot, Mats mats, Dictionary<string, Material> cache)
        {
            foreach (var mr in shellRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                if (AnomalyResolver.IsAnomalyWireName(n))
                {
                    Apply(mr, AnomSurf(n, mats), cache);
                    continue;                   // 关键：不给异常件补 Collider
                }
                var s = SurfFor(n, mats);
                if (s == null)
                {
                    // 兜底涂墙色只为画面不崩，不等于登记过了：Verify3D 那张独立的名单表会当场红。
                    Debug.LogError("[Bootstrap3D] 未登记的 base 名：" + n + "，先给 SurfaceRules 加一条");
                    s = mats.wall;
                }
                Apply(mr, s, cache);
                if (mr.GetComponent<Collider>() == null) mr.gameObject.AddComponent<BoxCollider>();
            }
        }

        /// <summary>
        /// base 网格名 → 表面。有序，第一条命中即生效，所以宽泛前缀必须排在具体前缀之后：
        /// Doorway（门斗里的黑）先于 Door（门板木头），LampBase（白灯座）先于 Lamp（自发光灯管），
        /// WindowFrame/WindowBar/WindowRail 先于 Window（窗外衬板）。
        /// 返回 null 表示这个名字没登记过——装配不猜，交给验收门的独立表判红。
        /// 名字口径的唯一真相在 blender/gen_corridor.py；改那里的命名要同步这张表和 Verify3D 的名单。
        /// </summary>
        static Surf SurfFor(string n, Mats m)
        {
            // 杂物一族单靠前缀查表，不进下面那条有序规则链：slug 是外部资产名，
            // 用 StartsWith 逐条写会把十三个名字焊死在这张表里，改一件道具要动两处。
            // 查不到就 return null 交给"未登记 base 名"那条门判红，不兜底涂墙色。
            if (n.StartsWith("Prop_"))
            {
                int cut = n.IndexOf("__", System.StringComparison.Ordinal);
                string slug = cut < 0 ? n.Substring(5) : n.Substring(5, cut - 5);
                Surf ps;
                if (m.props != null && m.props.TryGetValue(slug, out ps)) return ps;
            }
            if (n.EndsWith("_Handle")) return m.metal;                    // 门把手 / 铁门把手
            if (n.StartsWith("Doorway")) return m.black;                  // 门洞深处的黑，不是门板
            if (n.StartsWith("DoorFrame")) return m.wood;
            if (n.StartsWith("DoorPlate")) return m.paintedGreen;         // 搪瓷门牌
            if (n.StartsWith("Door")) return m.wood;
            if (n == "Glass_R") return m.glass;                           // 唯一透明件
            if (n.StartsWith("WindowFrame")) return m.paintedWhite;       // 旧钢窗刷白漆
            if (n.StartsWith("WindowBar") || n == "WindowRail") return m.metal;  // 防盗栏
            if (n.StartsWith("Window")) return m.window;                  // WindowBuildings 衬板
            if (n.StartsWith("LampBase")) return m.paintedWhite;
            if (n.StartsWith("Lamp")) return m.lampGlow;                  // 灯管自己发光（Unlit）
            if (n == "ExitSign") return m.green;                          // 安全出口牌常亮
            if (n.StartsWith("Wainscot")) return m.wainscot;
            if (n.StartsWith("Mailbox")) return m.paintedGreen;
            if (n.StartsWith("Poster")) return m.paper;
            if (n.StartsWith("Pipe") || n.StartsWith("CableTrunk")) return m.metal;
            if (n.StartsWith("FloorStrip")) return m.metal;               // 地面铜分格条
            if (n == "Floor") return m.floor;
            if (n == "Ceiling") return m.ceiling;
            if (n.StartsWith("EndDoor")) return m.paintedGreen;           // 楼梯间绿漆铁门
            if (n.StartsWith("Bike")) return m.metal;
            if (n.StartsWith("Wall") || n == "EndWall") return m.wall;
            return null;
        }

        /// <summary>异常件语义表：9 个 show 名里除人影外全部按"该长什么样"给材质。</summary>
        static Surf AnomSurf(string wire, Mats mats)
        {
            if (wire.Contains("emergencyLight")) return mats.green;   // 应急灯自己发光，暗走廊里才看得见
            if (wire.Contains("extraFigure")) return mats.black;
            if (wire.Contains("WaterPuddle")) return mats.water;       // 高 smoothness = 点光下反光的一滩水
            if (wire.Contains("RedWire")) return mats.red;
            if (wire.Contains("WindowBuildings")) return mats.black;   // 全黑，不是"变亮"
            if (wire.Contains("Door") || wire.Contains("Mailbox")) return mats.wood;
            if (wire.Contains("Shoes")) return mats.darkCloth;
            return mats.metal;                                      // 倒下的自行车＝金属件
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
        static void AttachBillboards(Transform shellRoot, Mats mats)
        {
            var fig = FindDeep(shellRoot, "anom02_show_extraFigure");
            if (fig == null) { Debug.LogWarning("[Bootstrap3D] 没找到 anom02_show_extraFigure，anom02 不会有人影。"); return; }
            var mr = fig.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mats.black.Mat;
            if (fig.GetComponent<ShadeFigure>() == null) fig.gameObject.AddComponent<ShadeFigure>();
        }

        /// <summary>
        /// 腿脚段（M7）：Body.fbx 的四件挂到 Player 根下，与相机**同级**。
        /// 不能挂相机下面——那样低头时腿跟着脸转，看起来像腿长在下巴上。
        /// 鞋在 FBX 里已经是腿的子节点（gen_body._shoe 里 parent 过），运行期只驱动两条腿、鞋跟着走，
        /// 这里不做任何拆装，原样收养层级。
        /// 材质按名分派单独一个函数，不进 SurfFor 那张规则表：那张表管的是"走廊构件长什么样"，
        /// 混进 StartsWith("Leg") 等于让建筑表兼管解剖；而身体件根本不在 Corridor(Clone) 树里，
        /// PaintShell 走不到它们。
        /// 补碰撞体一律不做：根上已有 CharacterController，给腿加 Collider 只会让玩家把自己推开。
        /// </summary>
        static void AssembleBody(Transform playerRoot, Mats mats, FirstPersonController fpc)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbxPath);
            if (asset == null)
            {
                Debug.LogError("[Bootstrap3D] 缺 " + BodyFbxPath + "，先跑 ./tools/blender_build.sh（没腿脚＝还在飘）");
                return;
            }
            var inst = Object.Instantiate(asset);
            inst.name = "Body";
            inst.transform.SetParent(playerRoot, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, FbxYaw, 0f); // 与走廊同一处轴向补偿：Blender +X（脚尖）→ Unity +Z（forward）

            foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var s = BodySurf(mr.name, mats);
                if (s == null) { Debug.LogError("[Bootstrap3D] 未登记的身体件名：" + mr.name); continue; }
                mr.sharedMaterial = s.Mat;
            }
            fpc.leftLeg = FindDeep(inst.transform, "Leg_L");
            fpc.rightLeg = FindDeep(inst.transform, "Leg_R");
            if (fpc.leftLeg == null || fpc.rightLeg == null)
                Debug.LogError("[Bootstrap3D] Body.fbx 里找不到 Leg_L/Leg_R，腿不会跟着迈步摆。");
        }

        static Surf BodySurf(string n, Mats m)
        {
            if (n.StartsWith("Leg")) return m.trousers;
            if (n.StartsWith("Shoe")) return m.shoes;
            return null;
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

        static void CreateHud(Camera cam)
        {
            var canvasGo = new GameObject("Hud3D", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Hud3D));
            var canvas = canvasGo.GetComponent<Canvas>();
            // spec §8 第 4 项裁定换 ScreenSpaceCamera：Overlay 模式的 UI 不进后处理链，
            // 梦核滤镜（Bloom/颜色分级/颗粒）吃不到 HUD，文字像贴在外面上、和走廊两个世界。
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            // planeDistance 必须 > camera.nearClipPlane(0.1f) 才不被裁掉；取 0.4f 是因为
            // CharacterController 默认半径 0.5f，玩家最多把相机送到离墙 ~0.5m，UI 不会扎进几何体被挡掉。
            canvas.planeDistance = 0.4f;
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

        /// <summary>一种表面 = 一份材质 + 它该多少米重复一次贴图。Tile&lt;=0 表示不参与 world-scale（纯色 Unlit 件）。</summary>
        private class Surf
        {
            public readonly Material Mat;
            public readonly float Tile;
            public Surf(Material mat, float tile) { Mat = mat; Tile = tile; }
        }

        private class Mats
        {
            public Surf floor, wall, wainscot, ceiling, wood, water, darkCloth, window, black, green, red;
            public Surf metal, paintedGreen, paintedWhite, paper, glass, lampGlow;
            public Surf trousers, shoes;                  // 腿脚段（M7）：身体件不吃贴图，纯色两层
            /// <summary>杂物段：键＝Prop_ 前缀后的 slug（实例名 Prop_&lt;slug&gt;__&lt;n&gt; 切掉后缀即得）。</summary>
            public Dictionary<string, Surf> props;
        }

        /// <summary>
        /// 表面表。贴图名与资产 id 解耦：代码只认 Content/Textures/&lt;dir&gt;/{BaseColor,Normal}.jpg，
        /// 换 ambientCG 资产改 fetch_textures.sh 的 IDS 与这里的 dir，不必动装配逻辑。
        /// tint 是乘在真实贴图上的，一律给接近白的色，深色 tint 会把走廊压成一片糊。
        /// </summary>
        static Mats MakeMats()
        {
            var m = new Mats();
            // M6 质感线：贴图目录与 fetch_textures.sh 的 IDS 一一对应，换资产只改这两处。
            // normalScale 单独成参是实机截图（logs/shots4/03）逼出来的：脏灰泥按 1.0 贴在 24m 墙面上
            // 把白灰墙读成毛石窑洞，梦核走廊要的是"刷过石灰的平墙"，不是浮雕。
            m.floor = Pbr("Floor", "Terrazzo002", new Color(0.86f, 0.84f, 0.80f), 0.50f, 2.0f, 0f, 0.70f);        // 水磨石
            m.wall = Pbr("Wall", "Plaster007", new Color(0.92f, 0.91f, 0.88f), 0.10f, 2.2f, 0f, 0.35f);           // 石灰白墙
            m.wainscot = Pbr("Wainscot", "Tiles032", new Color(0.95f, 1.00f, 0.97f), 0.55f, 1.0f, 0f, 0.60f);     // 墨绿釉面砖墙裙
            m.ceiling = Pbr("Ceiling", "Plaster007", new Color(0.95f, 0.94f, 0.92f), 0.08f, 2.2f, 0f, 0.35f);
            m.wood = Pbr("Wood", "Wood073", new Color(0.92f, 0.86f, 0.80f), 0.28f, 1.0f, 0f, 0.60f);              // 木门/门套
            // M6 结构段新增的五类表面。靠 tint/smoothness/metallic/tile 把钢门、栏杆、搪瓷牌、纸广告区分开。
            // metal 的 smoothness/metallic 同样被截图打回过一次：0.72/0.55 让 24m 长的线槽在每盏灯下
            // 拉出一道过曝高光带（logs/shots4/01 里那条"悬在空中的黑飘带"），哑光金属才贴得住筒子楼。
            m.metal = Pbr("Metal", "Concrete002", new Color(0.52f, 0.53f, 0.55f), 0.34f, 0.35f, 0.22f, 0.40f);    // 把手/防盗栏/管/分格条
            m.paintedGreen = Pbr("PaintedGreen", "PaintedMetal006", new Color(0.92f, 0.98f, 0.96f), 0.35f, 1.2f, 0.15f, 0.70f);  // 绿漆铁门/信箱/门牌
            m.paintedWhite = Pbr("PaintedWhite", "Plaster007", new Color(0.78f, 0.79f, 0.78f), 0.30f, 0.40f, 0f, 0.25f); // 白漆窗框/灯座
            m.paper = Pbr("Paper", "Paper003", new Color(0.90f, 0.88f, 0.82f), 0.12f, 0.5f, 0f, 0.50f);           // 小广告：揉皱的白纸
            m.water = Pbr("Water", "Concrete002", new Color(0.42f, 0.55f, 0.64f), 0.92f, 1.0f);      // 渗水：高光泽才有反光
            m.darkCloth = Pbr("DarkCloth", "Concrete002", new Color(0.30f, 0.24f, 0.22f), 0.05f, 0.5f);
            // 窗板自发光（Unlit 淡蓝灰）：夜里窗外比室内亮，这是"该有楼的地方"的正常态；
            // 异常态由 anom10 的黑面板盖上去，一亮一黑才有对比。
            m.window = new Surf(Unlit("WindowPale", new Color(0.62f, 0.70f, 0.78f)), 0f);
            // 灯管亮度：Unlit 的 RGB 就是最终出射值，原来的 (1.00,0.93,0.76) 在 Bloom 阈值之上，
            // 头顶那根 0.92m 的灯管被渲染成一整块过曝白斑（logs/shots4/01）；但一刀压到 0.62 又反过来
            // 成了天花板上一块卡其色硬纸板（logs/shots5/01）。0.86 是这两头的中间值：仍比四周被点光照到
            // 的墙面亮，"它是光源"这件事读得出来，但不再糊掉一片天花。
            m.lampGlow = new Surf(Unlit("LampGlow", new Color(0.86f, 0.80f, 0.64f)), 0f);              // 灯管自己亮
            m.black = new Surf(Unlit("ShadeBlack", new Color(0.02f, 0.02f, 0.02f)), 0f);
            m.green = new Surf(Unlit("EmergencyGreen", new Color(0.10f, 0.80f, 0.32f)), 0f);          // 应急灯/安全出口牌
            m.red = new Surf(Unlit("WireRed", new Color(0.78f, 0.10f, 0.08f)), 0f);                   // 红电线
            m.glass = Glass("GlassClear", new Color(0.72f, 0.80f, 0.86f, 0.30f));                     // 唯一透明件
            // 腿脚段（M7）：深靛蓝哑光裤腿 + 稍亮的皮鞋。带一点自发光不是因为风格，是因为照度——
            // 走廊只有六盏 2.75m 的点光，脚下 0.5m 处几乎没光，纯 Lit 的腿在实机帧里和地板同化成一片黑。
            // "腿很暗"是梦核，"看不见腿"是 bug。
            m.trousers = Solid("Trousers", new Color(0.13f, 0.14f, 0.19f), 0.22f, 0.06f);
            m.shoes = Solid("Shoes", new Color(0.20f, 0.16f, 0.13f), 0.55f, 0.05f);
            // 杂物段（M6 T2）：Poly Haven 的 CC0 道具，每件吃自己的 BaseColor/Normal
            // （tools/fetch_props.sh 落 Content/Textures/Prop_<slug>/）。
            // tileMeters 一律 0 ⇒ 不参与 world-scale：道具的 UV 是作者按整件铺满的，
            // 按米裁会把灭火器上的说明标签、挂钟的表盘撕成两半。
            // tint 用纯白：这些贴图本身就是实色实物，再乘一层色只会脏。
            // smoothness/metallic 按材质类别分开——塑料筐和铁皮柜在点光下的高光形态本来就不一样，
            // 全给同一个值会让十几种道具读起来像同一种注塑玩具。
            m.props = new Dictionary<string, Surf>();
            AddProp(m, "chair", 0.25f, 0f);            // 塑料面课椅
            AddProp(m, "bench", 0.20f, 0f);            // 油漆长凳（木头）
            AddProp(m, "table", 0.22f, 0f);
            AddProp(m, "medbox", 0.42f, 0.30f);        // 铁皮急救箱
            AddProp(m, "shelf", 0.40f, 0.45f);         // 金属置物架
            AddProp(m, "container", 0.35f, 0f);        // 塑料周转箱
            AddProp(m, "crate", 0.30f, 0f);
            AddProp(m, "sign", 0.50f, 0f);             // 警示牌要亮一点才跳出来
            AddProp(m, "bag", 0.45f, 0f);              // 黑色垃圾袋是亮塑料
            AddProp(m, "extinguisher", 0.65f, 0.35f);  // 红漆钢瓶：全走廊最亮的东西之一
            AddProp(m, "plunger", 0.15f, 0f);          // 橡胶头＋木柄，哑光
            AddProp(m, "clock", 0.30f, 0f);
            AddProp(m, "alarm", 0.40f, 0f);            // 火警按钮塑料壳
            return m;
        }

        static void AddProp(Mats m, string slug, float smoothness, float metallic)
        {
            m.props[slug] = Pbr("Prop_" + slug, "Prop_" + slug, Color.white, smoothness, 0f, metallic);
        }

        /// <summary>
        /// 梦核滤镜：把 Bloom/暗角/颗粒/调色/色散五道 override 真填进 VolumeProfile 资产。
        /// 幂等——已存在的 override 只调值不重复 Add（Bootstrap 每次 build.sh 都跑，
        /// 不判重就会每跑一次多存一份同名子资产）。属性名按 URP 17.0.1 包源码核实：
        /// ChromaticAberration 在这版是 ClampedFloatParameter intensity，不是旧版的 AnimationCurve amount；
        /// 五个类型全在 UnityEngine.Rendering.Universal 下（不是 UnityEngine.Rendering）。
        /// **关键**：VolumeProfile.Add 只 CreateInstance + 塞进 components 列表，
        /// 不做 AssetDatabase.AddObjectToAsset —— 光调它存出来的资产是 5 个 {fileID: 0} 空引用，
        /// 编辑器里看着有 override、构建产物里全是 null（和 URP Create() 丢 renderer 同一类坑）。
        /// 所以每道 override 必须自己挂进资产文件。
        /// </summary>
        static VolumeProfile EnsureDreamcoreProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (p == null) return null;

            // 旧版（漏了 AddObjectToAsset）留下的 fileID:0 空引用要先清掉：
            // Has/TryGet 内部对每个元素直接 .GetType()，留着它们下一次跑就 NullReferenceException。
            int junk = p.components.RemoveAll(c => c == null);
            if (junk > 0) Debug.Log("[Bootstrap3D] 清掉 " + junk + " 个空 override 引用（旧版没挂进资产文件）");

            var bloom = GetOrAddOverride<Bloom>(p);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.75f);
            bloom.scatter.Override(0.60f);
            bloom.highQualityFiltering.Override(true);

            var vig = GetOrAddOverride<Vignette>(p);
            vig.intensity.Override(0.38f);
            vig.smoothness.Override(0.45f);

            var grain = GetOrAddOverride<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.45f);
            grain.response.Override(0.60f);

            var ca = GetOrAddOverride<ColorAdjustments>(p);
            ca.postExposure.Override(-0.10f);
            ca.saturation.Override(-10f);   // 梦核是褪色的，不是浓艳的
            ca.contrast.Override(6f);

            var ab = GetOrAddOverride<ChromaticAberration>(p);
            ab.intensity.Override(0.22f);

            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>取或建一道 override；新建的那份必须挂成 Profile 的子资产，否则存盘是空引用。</summary>
        static T GetOrAddOverride<T>(VolumeProfile p)
            where T : VolumeComponent
        {
            if (p.TryGet<T>(out var c)) return c;
            c = p.Add<T>(true);
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static AudioClip LoadClip(string file)
        {
            var p = AudioRoot + "/" + file;
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (c == null) Debug.LogError("[Bootstrap3D] 缺音频 " + p + "，先跑 ./tools/make_audio.sh");
            return c;
        }

        static Texture2D LoadTex(string dir, string map)
        {
            var p = TexRoot + "/" + dir + "/" + map + ".jpg";
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (t == null) Debug.LogError("[Bootstrap3D] 缺贴图 " + p + "，先跑 "
                + (dir.StartsWith("Prop_") ? "./tools/fetch_props.sh" : "./tools/fetch_textures.sh"));
            return t;
        }

        /// <summary>法线图得先让导入器知道它是法线：默认按普通图导入会走 sRGB 采样、凹凸方向全错。</summary>
        static Texture2D LoadNormalTex(string dir)
        {
            var p = TexRoot + "/" + dir + "/Normal.jpg";
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null)
            {
                Debug.LogError("[Bootstrap3D] 缺法线贴图 " + p + "，先跑 "
                    + (dir.StartsWith("Prop_") ? "./tools/fetch_props.sh" : "./tools/fetch_textures.sh"));
                return null;
            }
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        static Surf Pbr(string name, string dir, Color tint, float smoothness, float tileMeters, float metallic = 0f, float normalScale = 1f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            var baseTex = LoadTex(dir, "BaseColor");
            var nrm = LoadNormalTex(dir);
            if (baseTex != null) m.SetTexture("_BaseMap", baseTex);
            if (nrm != null)
            {
                m.SetTexture("_BumpMap", nrm);
                m.SetFloat("_NormalScale", normalScale);
                // SetTexture 不会替你开关键词；漏 _NORMALMAP 时 URP Lit 直接不采样法线，日志和资产断言全绿、画面却是平的。
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetColor("_BaseColor", tint);   // URP Lit 的基色属性是 _BaseColor，Material.color 那个 _Color 在这套 shader 里不存在
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            return new Surf(m, tileMeters);
        }

        /// <summary>
        /// 透明玻璃。URP 17 的 Lit 不是"设个 alpha 就透明"——混合状态由 6 个隐藏 float + 一个关键词
        /// 共同决定，属性名按包源码核实（Shaders/Lit.shader 的 Properties 与
        /// Editor/ShaderGUI/BaseShaderGUI.cs:1018 MaterialPropertyBlock 那段真值表）：
        /// _Surface=1(Transparent) 才开 _SURFACE_TYPE_TRANSPARENT，Alpha 混合下
        /// _SrcBlend=SrcAlpha / _DstBlend=OneMinusSrcAlpha / _ZWrite=0，再把 renderQueue 提到透明队列。
        /// 少任何一项都会得到"看着像 opaque 的灰板"，而材质列表和日志全都一切正常。
        /// </summary>
        static Surf Glass(string name, Color tintWithAlpha)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);                                   // BlendMode.Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 2f);                                    // 单面玻璃用 Back，双面会重影
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.92f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", tintWithAlpha);
            return new Surf(m, 0f);     // Tile<=0：不参与 world-scale（玻璃不铺贴图）
        }

        /// <summary>
        /// 纯色 Lit（身体件用：不吃贴图，也就不会触发"缺贴图"那条 LogError）。
        /// emission 是乘在基色上的自发光系数。**_EMISSION 关键词必须手动开**：
        /// 只 SetColor("_EmissionColor") 的话 URP Lit 根本不采样发光，资产、日志、画面三者里只有
        /// 最后一样不对——和 _NORMALMAP 是同一类坑（见 Pbr 的注释）。
        /// </summary>
        static Surf Solid(string name, Color c, float smoothness, float emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            return new Surf(m, 0f);     // Tile<=0：不参与 world-scale
        }

        static Material Unlit(string name, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            m.SetColor("_BaseColor", c);
            m.color = c;
            return m;
        }

        /// <summary>
        /// world-scale 贴图。Blender 原立方体**每面 UV 都铺满整个 0–1**，`transform_apply(scale=True)`
        /// 只改顶点不改 UV（blender/probe_uv.py 实测：25 个网格各 1 个 UV 层），所以 24m 的地面和
        /// 0.18m 的布鞋会各画一遍同样的贴图。这里按世界尺寸补 tiling：取 bounds 三条边里最长的两条
        /// （最短的是板厚），除以该表面的 tile 米数。尺寸只认 Renderer.bounds——FBX 导入节点上
        /// 只有世界量是米（spec §9.2，Mesh.bounds×lossyScale 数值对但轴不对）。
        /// </summary>
        static void Apply(MeshRenderer mr, Surf s, Dictionary<string, Material> cache)
        {
            if (s == null || s.Mat == null) return;
            if (s.Tile <= 0f) { mr.sharedMaterial = s.Mat; return; }
            var v = mr.bounds.size;
            float hi = Mathf.Max(v.x, Mathf.Max(v.y, v.z));
            float lo = Mathf.Min(v.x, Mathf.Min(v.y, v.z));
            float mid = v.x + v.y + v.z - hi - lo;
            float a = Mathf.Max(hi, mid), b = Mathf.Min(hi, mid);
            if (b < 0.05f) { mr.sharedMaterial = s.Mat; return; }  // 读不到有效世界量就不猜 tile
            float sx = Mathf.Max(1f, a / s.Tile), sy = Mathf.Max(1f, b / s.Tile);
            string key = s.Mat.name + "|" + Mathf.RoundToInt(sx * 100f) + "x" + Mathf.RoundToInt(sy * 100f);
            Material mat;
            if (!cache.TryGetValue(key, out mat))
            {
                mat = new Material(s.Mat) { name = key };
                mat.SetTextureScale("_BaseMap", new Vector2(sx, sy));
                mat.SetTextureScale("_BumpMap", new Vector2(sx, sy));
                cache[key] = mat;
            }
            mr.sharedMaterial = mat;
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
