using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamcore
{
    /// <summary>
    /// 全部 UI 绑定。挂在 Canvas 上，按 Bootstrap 生成的命名层级自绑定（路径约定见 Bootstrap）。
    /// 层级：SpotImage → Scanlines → BlackOverlay → HudPanel → TitlePanel → EndingPanel。
    /// </summary>
    public class UiController : MonoBehaviour
    {
        Canvas canvas;
        GameObject titlePanel, hudPanel, endingPanel;
        RawImage spotImage;
        Text depthText, mistakesText, titleText, endingText;
        Button btnForward, btnBack, btnStart, btnRestart;

        Coroutine crossfade;

        void Awake()
        {
            canvas = GetComponent<Canvas>();

            spotImage = Find<RawImage>("SpotImage");
            depthText = Find<Text>("HudPanel/DepthText");
            mistakesText = Find<Text>("HudPanel/MistakesText");
            btnForward = Find<Button>("HudPanel/BtnForward");
            btnBack = Find<Button>("HudPanel/BtnBack");
            titlePanel = FindGo("TitlePanel");
            titleText = Find<Text>("TitlePanel/TitleText");
            btnStart = Find<Button>("TitlePanel/BtnStart");
            endingPanel = FindGo("EndingPanel");
            endingText = Find<Text>("EndingPanel/EndingText");
            btnRestart = Find<Button>("EndingPanel/BtnRestart");
            hudPanel = FindGo("HudPanel");

            if (btnForward != null) btnForward.onClick.AddListener(() => Report(true));
            if (btnBack != null) btnBack.onClick.AddListener(() => Report(false));
            if (btnStart != null) btnStart.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.StartGame(); });
            if (btnRestart != null) btnRestart.onClick.AddListener(() => { if (GameManager.Instance != null) GameManager.Instance.Restart(); });
        }

        T Find<T>(string path) where T : Component
        {
            Transform t = canvas != null ? canvas.transform.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        GameObject FindGo(string path)
        {
            Transform t = canvas != null ? canvas.transform.Find(path) : null;
            return t != null ? t.gameObject : null;
        }

        void Report(bool forward)
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
                GameManager.Instance.loop.Report(forward);
        }

        public void ShowTitle()
        {
            SetPanel(titlePanel, true, hudPanel, false, endingPanel, false);
        }

        public void ShowPlaying()
        {
            SetPanel(titlePanel, false, hudPanel, true, endingPanel, false);
            UpdateHud();
        }

        void SetPanel(GameObject title, bool t, GameObject hud, bool h, GameObject ending, bool e)
        {
            if (title != null) title.SetActive(t);
            if (hud != null) hud.SetActive(h);
            if (ending != null) ending.SetActive(e);
        }

        public void ShowSpot(Spot spot, int anomalyIndex)
        {
            Sprite s = (anomalyIndex >= 0 && anomalyIndex < spot.anomalies.Count)
                ? spot.anomalies[anomalyIndex] : spot.normal;
            if (crossfade != null) StopCoroutine(crossfade);
            crossfade = StartCoroutine(Crossfade(s));
            UpdateHud();
        }

        IEnumerator Crossfade(Sprite s)
        {
            if (spotImage == null) yield break;
            Color c = spotImage.color;
            const float fadeOut = 0.22f, fadeIn = 0.35f;
            for (float t = 0; t < fadeOut; t += Time.deltaTime)
            {
                c.a = 1f - t / fadeOut;
                spotImage.color = c;
                yield return null;
            }
            if (s != null) spotImage.texture = s.texture;
            for (float t = 0; t < fadeIn; t += Time.deltaTime)
            {
                c.a = t / fadeIn;
                spotImage.color = c;
                yield return null;
            }
            c.a = 1f;
            spotImage.color = c;
        }

        void UpdateHud()
        {
            LoopController loop = GameManager.Instance != null ? GameManager.Instance.loop : null;
            if (loop == null) return;
            if (depthText != null) depthText.text = "地下 " + loop.Depth + " 层";
            if (mistakesText != null) mistakesText.text = "异常记录 " + loop.Mistakes + "/" + loop.maxMistakes;
        }

        public void ShowEnding(bool win, int depth, int mistakes)
        {
            SetPanel(titlePanel, false, hudPanel, false, endingPanel, true);
            if (endingText != null)
                endingText.text = win
                    ? "你出来了。\n天快亮了，楼道里还是那股消毒水的味道。"
                    : "你没能出来。\n它还在原地等你，等下一个回来的孩子。";
        }
    }
}
