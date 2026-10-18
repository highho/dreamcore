using System.Collections.Generic;
using System.IO;
using Dreamcore;
using Dreamcore.D3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// M3 出口条件的取证工装（batchmode -executeMethod Dreamcore.EditorTools.CaptureAnomalies.Run）。
    /// 对 10 组异常各渲染"出现前/出现后"两帧 640×360 PNG 到 logs/captures/，
    /// 用真几何、真贴图、真灯位回答"这一组异常在 24m 走廊里到底看不看得出来"。
    ///
    /// 已知边界（不算缺陷，如实登记）：
    /// - **编辑态静态渲染**：不进 Play Mode（batchmode 进不去），所以 `LightFlicker` 不闪、
    ///   Volume 的后处理链是否生效取决于 URP 对非玩法相机的求值。实机那一路由 build 后的截图另证。
    /// - 相机按异常件世界坐标反推站位（沿走廊退 STEP_BACK 米），不代表玩家真实走位。
    /// </summary>
    public static class CaptureAnomalies
    {
        const string Scene3DPath = "Assets/_Project/Scenes/Main3D.unity";
        const float EyeHeight = 1.6f;
        const float StepBack = 4.0f;   // 沿 -Z 退这么多米去看，corridor 宽 2.9m，再近就只剩贴图纹理
        const int W = 640, H = 360;

        [MenuItem("Dreamcore/Capture anomalies (M3 取证)")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scene3DPath, OpenSceneMode.Single);
            var loop = Object.FindFirstObjectByType<Loop3D>();
            if (loop == null) { Debug.LogError("[CAPTURE] FAIL: Main3D 里没有 Loop3D，先跑 Bootstrap3D.Run"); return; }
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[CAPTURE] FAIL: 没有 Main Camera"); return; }

            // 落点用 Application.dataPath 反推工程根，不用 Directory.GetCurrentDirectory()：
            // batchmode 下后者是 Unity.exe 自己的目录，PNG 会写进编辑器安装目录。
            string dir = Path.GetFullPath(Application.dataPath + "/../../logs/captures");
            Directory.CreateDirectory(dir);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var read = new Texture2D(W, H, TextureFormat.RGB24, false);
            // 不用 Tuple：`using UnityEngine` 下它解析到 UnityEngine.Tuple（不可访问），不是 System.Tuple
            Vector3 originalPos = cam.transform.position;
            Quaternion originalRot = cam.transform.rotation;

            // 正常态（depth 1 的记忆锚）站在走廊中段拍一张，作为"玩家见过的正常"参照
            Place(cam, new Vector3(0f, EyeHeight, 8f), new Vector3(0f, 1.5f, 20f));
            Save(cam, rt, read, dir + "/00_clean_mid.png");
            Place(cam, new Vector3(0f, EyeHeight, 8f), new Vector3(-1.6f, 1.5f, 9f));
            Save(cam, rt, read, dir + "/00_clean_window.png");

            foreach (var e in loop.anomalies)
            {
                var target = PickTarget(e);
                if (target == null)
                {
                    Debug.LogError("[CAPTURE] FAIL: " + e.id + " 找不到可瞄准的物体");
                    continue;
                }
                Vector3 look = target.Value;
                Place(cam, new Vector3(0f, EyeHeight, Mathf.Max(1.6f, look.z - StepBack)), look);

                string before = dir + "/" + e.id + "_before.png";
                Save(cam, rt, read, before);

                e.Apply();
                string after = dir + "/" + e.id + "_after.png";
                Save(cam, rt, read, after);
                e.Revert();

                Debug.Log("[CAPTURE] " + e.id + " " + e.title + " 瞄准=" + look + " -> " + after);
            }

            cam.transform.SetPositionAndRotation(originalPos, originalRot);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(read);
            Debug.Log("[CAPTURE] PASS: 10 组异常前后对比帧已写入 logs/captures/");
        }

        /// <summary>
        /// 瞄准点：优先 show 物件本体（异常出现的东西）；纯"消失型"异常（anom04 三张广告都不在 show 里）
        /// 退到 hide 指认的 base 目标位置——那里正是玩家该看出"少了东西"的地方。
        /// 位置一律取 MeshRenderer.bounds.center，**不取 transform.position**：Blender 导出把物体变换
        /// 烘进顶点（logs/verify-fbx.log 实测 mesh 节点读 (0,0,0)、而 Floor 的 bounds 在 z≈12），
        /// 只有世界量可信（spec §9.2）。EMPTY 灯位锚没有 MeshRenderer，退回 transform.position。
        /// </summary>
        static Vector3? PickTarget(AnomalyEntry e)
        {
            GameObject go = null;
            if (e.show != null && e.show.Count > 0 && e.show[0] != null) go = e.show[0];
            else if (e.hide != null && e.hide.Count > 0 && e.hide[0] != null) go = e.hide[0];
            if (go == null) return null;
            var mr = go.GetComponent<MeshRenderer>();
            Vector3 p = mr != null ? mr.bounds.center : go.transform.position;
            Debug.Log("[CAPTURE] 取点 " + go.name + " 途径=" + (mr != null ? "bounds.center" : "transform.position")
                      + " -> " + p);
            return p;
        }

        static void Place(Camera cam, Vector3 pos, Vector3 look)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
        }

        static void Save(Camera cam, RenderTexture rt, Texture2D read, string path)
        {
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            read.Apply();
            RenderTexture.active = prevActive;
            cam.targetTexture = prevTarget;
            File.WriteAllBytes(path, read.EncodeToPNG());
        }
    }
}
