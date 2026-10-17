// dreamcore/Assets/_Project/Editor/Verify3D.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// 自验收口（batchmode -executeMethod）。约定：日志只认 [VERIFY] PASS / [VERIFY] FAIL。
    /// 坏样本开关：环境变量 VERIFY_BAD=1 时，期望集里塞进一个绝不存在的名，
    /// 用来证明这个检查器真的会失败——不被喂坏样本之前，它的 PASS 不算背书。
    /// </summary>
    public static class Verify3D
    {
        const string ProbePath = "Assets/Content/3D/Probe.fbx";

        public static void ProbeHierarchy()
        {
            var expected = new List<string> { "wallL", "floor", "shadeFigure", "floor_off" };
            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                expected.Add("no_such_object_xyz");

            AssetDatabase.ImportAsset(ProbePath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProbePath);
            if (prefab == null)
            {
                Debug.LogError("[VERIFY] FAIL: 无法加载 " + ProbePath);
                return;
            }

            var paths = new List<string>();
            var objectNames = new HashSet<string>();
            var sb = new StringBuilder();
            Walk(prefab.transform, "", paths, objectNames, sb);
            Debug.Log("[VERIFY] ---- 层级实测 ----\n" + sb);

            bool collNodes = false;
            foreach (var p in paths)
                // 转录修正②：FBX 资产根节点（名为 Probe）必然占据路径首段，
                // 计划原文的 p.StartsWith("base") 只可能恒为 False，测不到想测的东西。
                // 这里按"路径段等于 base / anom"判定，问的还是同一个问题。
                foreach (var seg in p.Split('/'))
                    if (seg == "base" || seg == "anom") collNodes = true;
            Debug.Log("[VERIFY] 集合是否成为节点: " + collNodes);

            var missing = new List<string>();
            foreach (var name in expected)
                if (!objectNames.Contains(name)) missing.Add(name);

            // 尺度探针：floor 与 wallL 的 z 差应≈10（1 Blender unit = 1 Unity meter）
            // 证据补行（不改判定）：Blender 是 Z-up、Unity 是 Y-up，导入会重排轴，
            // 所以 z差 可能读到 0 —— 那不代表缩放错，必须把实际向量也打进日志才能定倍率。
            Transform tWall = FindByName(prefab.transform, "wallL");
            Transform tFloor = FindByName(prefab.transform, "floor");
            if (tWall != null && tFloor != null)
            {
                Debug.Log("[VERIFY] 尺度 z差=" + Mathf.Abs(tFloor.position.z - tWall.position.z)
                          + "（期望≈10）");
                Debug.Log("[VERIFY] 实测位置 wallL=" + tWall.position
                          + " floor=" + tFloor.position
                          + " 距离=" + Vector3.Distance(tWall.position, tFloor.position)
                          + " rootScale=" + prefab.transform.localScale
                          + " rootRot=" + prefab.transform.localEulerAngles);
            }

            if (missing.Count == 0)
                Debug.Log("[VERIFY] PASS: 期望对象名全部保真 " + string.Join(",", expected));
            else
                Debug.LogError("[VERIFY] FAIL: 缺失 " + string.Join(",", missing));
        }

        /// <summary>
        /// 只有带网格的节点算"物体"，空节点是 Blender 集合的残影。
        /// 两个集合分开统计，才能分别回答"名字保真吗"和"层级还在吗"。
        /// </summary>
        static void Walk(Transform t, string prefix, List<string> paths,
                         HashSet<string> objectNames, StringBuilder sb)
        {
            string path = string.IsNullOrEmpty(prefix) ? t.name : prefix + "/" + t.name;
            paths.Add(path);
            bool isMesh = t.GetComponent<MeshFilter>() != null;
            if (isMesh) objectNames.Add(t.name);
            sb.AppendLine(path + (isMesh ? "  <mesh>" : "  <node>"));
            // 转录修正①：计划原文此处漏传 paths，四个实参对不上五参签名，编译不过。
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), path, paths, objectNames, sb);
        }

        static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindByName(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
