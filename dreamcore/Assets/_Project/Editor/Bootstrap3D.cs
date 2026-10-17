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
    /// 3D 轨道一键引导（与 2D 模板完全隔离，不动 Main.unity）：
    ///   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run
    /// 生成 Main3D.unity：第一人称 + 筒子楼灰盒走廊 + 灯闪 + 黑影贴片 + 两端判定门 + 复用梦核滤镜 + HUD。
    ///
    /// 构建入口切换（jam 中空余时间启用 3D）：
    ///   -executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate3D   → 后续 build 出 3D 版
    ///   -executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate2D   → 切回 2D 版
    ///
    /// 走廊尺寸（中式筒子楼）：宽 2.4m × 高 3m × 长 24m，玩家沿 +Z 行走。
    /// </summary>
    public static class Bootstrap3D
    {
        const string Scene3DPath = "Assets/_Project/Scenes/Main3D.unity";
        const string Scene2DPath = "Assets/_Project/Scenes/Main.unity";
        const string ProfilePath = "Assets/_Project/Settings/VolumeProfile-Dreamcore.asset";
        const string FontPath = "Assets/_Project/Fonts/simhei.ttf";

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

        public static void Activate2D()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene2DPath, true) };
            Debug.Log("[Bootstrap3D] 构建入口已切回 2D（Main）。");
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

            // 材质
            Material matFloor = MakeMat("Floor", new Color(0.30f, 0.28f, 0.25f));
            Material matWall = MakeMat("Wall", new Color(0.55f, 0.54f, 0.50f));
            Material matWainscot = MakeMat("Wainscot", new Color(0.22f, 0.38f, 0.26f)); // 绿漆墙裙
            Material matCeil = MakeMat("Ceiling", new Color(0.62f, 0.61f, 0.58f));
            Material matBlack = MakeUnlit("ShadeBlack", new Color(0.02f, 0.02f, 0.02f));

            float halfW = CorridorWidth * 0.5f;
            float halfL = CorridorLength * 0.5f;

            MakeBox("Floor", root, new Vector3(0f, -0.05f, halfL), new Vector3(CorridorWidth, 0.1f, CorridorLength), matFloor);
            MakeBox("WallLeft", root, new Vector3(-halfW, CorridorHeight * 0.5f, halfL), new Vector3(0.1f, CorridorHeight, CorridorLength), matWall);
            MakeBox("WallRight", root, new Vector3(halfW, CorridorHeight * 0.5f, halfL), new Vector3(0.1f, CorridorHeight, CorridorLength), matWall);
            MakeBox("Ceiling", root, new Vector3(0f, CorridorHeight + 0.05f, halfL), new Vector3(CorridorWidth, 0.1f, CorridorLength), matCeil);
            MakeBox("EndWall", root, new Vector3(0f, CorridorHeight * 0.5f, CorridorLength), new Vector3(CorridorWidth, CorridorHeight, 0.1f), matWall);
            MakeBox("WainscotL", root, new Vector3(-halfW + 0.055f, 0.6f, halfL), new Vector3(0.01f, 1.2f, CorridorLength), matWainscot);
            MakeBox("WainscotR", root, new Vector3(halfW - 0.055f, 0.6f, halfL), new Vector3(0.01f, 1.2f, CorridorLength), matWainscot);

            // 灯（暖黄点光 + 灯闪）
            var lights = new List<LightFlicker>();
            for (float z = 2f; z <= CorridorLength - 2f; z += 4f)
            {
                var lg = new GameObject("Light_" + (int)z);
                lg.transform.SetParent(root, false);
                lg.transform.position = new Vector3(0f, CorridorHeight - 0.25f, z);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.85f, 0.60f);
                l.intensity = 1.8f;
                l.range = 7f;
                l.shadows = LightShadows.None;
                lights.Add(lg.AddComponent<LightFlicker>());
            }

            // 黑影贴片（默认隐藏，异常时激活）
            var shades = new List<ShadeFigure>();
            foreach (var (x, z) in new[] { (0.6f, 8f), (-0.7f, 14f), (0.55f, 20f) })
            {
                var q = MakeQuad("Shade_" + (int)z, root, new Vector3(x, 0.9f, z), matBlack);
                var mr = q.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                q.transform.localScale = new Vector3(0.8f, 1.8f, 1f);
                shades.Add(q.AddComponent<ShadeFigure>());
            }

            // 判定门：起点=回头，尽头=前进
            MakeGate("GateBack", root, new Vector3(0f, 1.5f, 1.2f), false);
            MakeGate("GateForward", root, new Vector3(0f, 1.5f, CorridorLength - 1.2f), true);

            // Player：根(CharacterController) + 子相机
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.1f, 1.8f);
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

            // HUD（Overlay：文字清晰，场景由 URP 滤镜盖住）
            CreateHud();

            // 事件系统
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, Scene3DPath);
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

        static GameObject MakeQuad(string name, Transform parent, Vector3 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 不挡路
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
