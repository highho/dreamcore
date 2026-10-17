using UnityEngine;

namespace Dreamcore
{
    public enum GameState { Title, Playing, Ending }

    /// <summary>
    /// 全局状态机：Title（标题）→ Playing（找异常 loop）→ Ending（好/坏结局）。
    /// 单例，挂在场景里由 Bootstrap 生成的 "GameManager" 物体上。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References（Bootstrap 自动绑定，缺省时 Start 兜底查找）")]
        public SpotsCatalog catalog;
        public LoopController loop;
        public UiController ui;
        public AudioController audio;

        public GameState State { get; private set; } = GameState.Title;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (loop == null) loop = GetComponent<LoopController>();
            if (ui == null) ui = FindObjectOfType<UiController>();
            if (audio == null) audio = GetComponent<AudioController>();
            if (catalog == null) catalog = Resources.Load<SpotsCatalog>("SpotsCatalog");
            ui.ShowTitle();
        }

        public void StartGame()
        {
            State = GameState.Playing;
            loop.Begin(catalog);
            ui.ShowPlaying();
        }

        public void OnGameOver(bool win)
        {
            State = GameState.Ending;
            ui.ShowEnding(win, loop.Depth, loop.Mistakes);
        }

        public void Restart()
        {
            State = GameState.Title;
            ui.ShowTitle();
        }
    }
}
