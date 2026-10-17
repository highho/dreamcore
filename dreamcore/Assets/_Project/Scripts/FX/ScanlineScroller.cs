using UnityEngine;
using UnityEngine.UI;

namespace Dreamcore
{
    /// <summary>
    /// CRT 扫描线：程序化生成条纹纹理 + UV 滚动，零外部贴图、零 shader。
    /// 挂在 Scanlines 物体上（RawImage，全屏，位于 SpotImage 之上）。
    /// </summary>
    public class ScanlineScroller : MonoBehaviour
    {
        public RawImage target;
        public float scrollSpeed = 6f;      // UV 滚动速度（向下滚）
        [Range(0f, 1f)] public float lineAlpha = 0.13f;
        public float linePeriod = 4f;       // 每 N 像素一条暗线（4 = 细腻，2 = 粗糙 CRT）

        void Awake()
        {
            if (target == null) target = GetComponent<RawImage>();
            if (target != null) target.texture = GenerateScanlineTexture();
        }

        void Update()
        {
            if (target == null || target.texture == null) return;
            Rect r = target.uvRect;
            r.y -= Time.deltaTime * scrollSpeed * 0.01f;
            target.uvRect = r;
        }

        Texture2D GenerateScanlineTexture()
        {
            const int w = 256, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "Scanlines(procedural)";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                bool dark = (y % (int)linePeriod) < 1;
                for (int x = 0; x < w; x++)
                    px[y * w + x] = dark
                        ? new Color32(0, 0, 0, (byte)(lineAlpha * 255f))
                        : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
