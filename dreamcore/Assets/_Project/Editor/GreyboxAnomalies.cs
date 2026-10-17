// dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs
using System.Collections.Generic;
using UnityEngine;
using Dreamcore;
using Dreamcore.D3;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// 用图元造出 base + anom&lt;NN&gt;_show/hide 前缀式灰盒，名字严格按 AnomalyResolver 的
    /// 前缀语法（^anom\d{2}_(show|hide)_...$，_off 后缀指认 base 目标）。
    /// 只在 M1（还没有 FBX）期间存在；Task 7 换成实例化 FBX 后整个文件删除。
    /// hide 节点下只放"名字载体"（空 GameObject），与 FBX 里的 EMPTY 同构；
    /// show/hide/&lt;NN&gt;/anom 等父节点纯为编排脚手架，AnomalyResolver 只按对象自身名分桶、忽略它们。
    /// 实测：Blender 集合进不了 FBX（spec §9.1），异常语义只能活在对象名前缀里，故此处叶子一律自带全 wire 名。
    /// </summary>
    public static class GreyboxAnomalies
    {
        const float HalfW = 1.1f;   // 贴墙摆放（走廊宽 2.4）
        const float BackZ = 23.4f;

        public static void Build(Transform root, IReadOnlyList<LightFlicker> lights)
        {
            groups.Clear();               // 静态缓存，每次重建场景都要归零
            anomRoot = New("anom", root).transform;
            var b = New("base", root).transform;

            // 可被 hide 指认的本体（裸名，供 hide 代理剥 _off 后按名指认）
            Prop("Poster_6", b, new Vector3(-HalfW, 1.5f, 6f), new Vector3(0.02f, 0.7f, 0.5f), C(0.86f, 0.83f, 0.72f));
            Prop("Poster_10", b, new Vector3(HalfW, 1.5f, 10f), new Vector3(0.02f, 0.7f, 0.5f), C(0.86f, 0.83f, 0.72f));
            Prop("Poster_18", b, new Vector3(-HalfW, 1.45f, 18f), new Vector3(0.02f, 0.6f, 0.44f), C(0.90f, 0.86f, 0.70f));
            Prop("Door_3", b, new Vector3(-HalfW, 1.05f, 3.4f), new Vector3(0.04f, 2.1f, 0.9f), C(0.42f, 0.30f, 0.22f));
            Prop("MailboxDoor", b, new Vector3(HalfW, 1.25f, 12.6f), new Vector3(0.03f, 0.75f, 0.95f), C(0.48f, 0.49f, 0.52f));
            Prop("BikeUp", b, new Vector3(HalfW - 0.25f, 0.55f, 15.5f), new Vector3(0.55f, 1.1f, 0.2f), C(0.30f, 0.32f, 0.36f));
            Prop("WindowBuildings", b, new Vector3(HalfW + 0.4f, 1.9f, 9f), new Vector3(0.02f, 1.6f, 3.4f), C(0.62f, 0.66f, 0.74f));

            // anom01 声控灯全灭，只剩绿色应急光：show=低压应急灯；hide 逐盏灯名指认
            var g01s = Show("01"); var g01h = Hide("01");
            var emerg = New("anom01_show_emergencyLight", g01s);
            emerg.transform.localPosition = new Vector3(0f, 0.35f, 12f);
            var el = emerg.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.25f, 0.9f, 0.35f);
            el.intensity = 0.9f;
            el.range = 6f;
            // LightFlicker 与 Light 同在灯 GameObject 上，name 已是 Light_<int>；每盏真灯派生一个 hide 代理
            foreach (var lf in lights)
                Proxy("anom01_hide_" + lf.gameObject.name + "_off", g01h, lf.transform.position);

            // anom02 尽头多出背对镜头的人影（纯 show）
            var fig = Prop("anom02_show_extraFigure", Show("02"), new Vector3(0f, 0.9f, BackZ - 0.6f),
                new Vector3(0.8f, 1.8f, 1f), C(0.02f, 0.02f, 0.02f));
            Object.DestroyImmediate(fig.GetComponent<Collider>());
            fig.AddComponent<ShadeFigure>();

            // anom03 一户防盗门半开（closed ↔ open 互换）
            Swap("03", b, "Door_3", new Vector3(-HalfW + 0.42f, 1.05f, 3.4f),
                new Vector3(0.9f, 2.1f, 0.04f), C(0.42f, 0.30f, 0.22f));

            // anom04 开锁小广告整排消失（纯 hide，无 show）
            var h04 = Hide("04");
            Proxy("anom04_hide_Poster_6_off", h04, new Vector3(-HalfW, 1.5f, 6f));
            Proxy("anom04_hide_Poster_10_off", h04, new Vector3(HalfW, 1.5f, 10f));
            Proxy("anom04_hide_Poster_18_off", h04, new Vector3(-HalfW, 1.45f, 18f));

            // anom05 地上多出一双小孩旧布鞋（纯 show）
            Prop("anom05_show_Shoes", Show("05"), new Vector3(-0.35f, 0.04f, 7.2f), new Vector3(0.42f, 0.08f, 0.18f), C(0.35f, 0.22f, 0.18f));

            // anom06 靠墙的旧自行车倒了
            Swap("06", b, "BikeUp", new Vector3(HalfW - 0.15f, 0.22f, 15.5f),
                new Vector3(1.05f, 0.42f, 0.5f), C(0.30f, 0.32f, 0.36f));

            // anom07 信报箱小门全部敞开
            Swap("07", b, "MailboxDoor", new Vector3(HalfW - 0.32f, 1.25f, 12.6f),
                new Vector3(0.65f, 0.75f, 0.03f), C(0.48f, 0.49f, 0.52f));

            // anom08 门缝下渗出一滩水（纯 show）
            var water = Prop("anom08_show_WaterPuddle", Show("08"), new Vector3(-HalfW + 0.35f, 0.02f, 3.4f),
                new Vector3(0.7f, 0.01f, 0.45f), C(0.10f, 0.12f, 0.14f));
            water.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.10f, 0.12f, 0.14f) };

            // anom09 天花板垂下一段红电线（纯 show）
            Prop("anom09_show_RedWire", Show("09"), new Vector3(0.3f, 2.45f, 17.2f), new Vector3(0.035f, 1.1f, 0.035f), C(0.62f, 0.10f, 0.10f));

            // anom10 窗外该有楼的地方全黑
            Swap("10", b, "WindowBuildings", new Vector3(HalfW + 0.4f, 1.9f, 9f),
                new Vector3(0.02f, 1.6f, 3.4f), C(0.01f, 0.01f, 0.015f));
        }

        /// <summary>base 里换掉一个：show 自带 anom&lt;NN&gt;_show_&lt;目标名&gt;_open；
        /// hide 自带 anom&lt;NN&gt;_hide_&lt;目标名&gt;_off（剥 _off 后指认 base 目标）。</summary>
        static void Swap(string id, Transform baseNode, string targetName, Vector3 pos, Vector3 scale, Color col)
        {
            Prop("anom" + id + "_show_" + targetName + "_open", Show(id), pos, scale, col);
            Proxy("anom" + id + "_hide_" + targetName + "_off", Hide(id), baseNode.Find(targetName).position);
        }

        // ---------- 层级与图元 ----------

        static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();

        static Transform Show(string id) { return Group(id, "show"); }
        static Transform Hide(string id) { return Group(id, "hide"); }

        static Transform Group(string id, string leaf)
        {
            string key = id + "/" + leaf;
            Transform t;
            if (groups.TryGetValue(key, out t)) return t;
            var idNode = GroupRoot(id);
            t = New(leaf, idNode).transform;
            groups[key] = t;
            return t;
        }

        static Transform GroupRoot(string id)
        {
            Transform t;
            if (groups.TryGetValue("id/" + id, out t)) return t;
            t = New(id, anomRoot).transform;
            groups["id/" + id] = t;
            return t;
        }

        static Transform anomRoot;

        static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject Prop(string name, Transform parent, Vector3 pos, Vector3 scale, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_mat", color = col };
            return go;
        }

        static void Proxy(string name, Transform parent, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b); }
    }
}
