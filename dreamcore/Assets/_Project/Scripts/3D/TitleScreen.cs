using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dreamcore.D3
{
    /// <summary>
    /// 标题页（Title.unity 唯一的玩法脚本，由 Bootstrap3D.CreateTitleScene 装配）。
    /// 光标必须解锁：Main3D 里 FirstPersonController.Start 把它锁上，标题页正好相反——
    /// 锁着光标点不到按钮，玩家会以为界面死了。
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public Button startButton;
        public Button quitButton;
        public string gameSceneName = "Main3D";

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 监听必须在运行期挂：onClick 的 AddListener 不进场景序列化（存盘只留 Inspector 里加的
            // m_PersistentCalls），装配脚本里挂的那份一存盘就没了。
            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        void Update()
        {
            // 键盘也要能开局：实机取证工装只会合成按键，不会点 UI 控件。
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space))
                StartGame();
            else if (Input.GetKeyDown(KeyCode.Escape)) Quit();
        }

        public void StartGame()
        {
            // 按钮点击与按键可能在同一帧各来一次，LoadScene 重复调用是最后一次生效，不会叠两层。
            SceneManager.LoadScene(gameSceneName);
        }

        public void Quit()
        {
            Application.Quit();
        }
    }
}
