using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 第一人称控制器（legacy input，零新包依赖）。
    /// 挂在 Player 根物体（含 CharacterController）；子相机做 pitch，根做 yaw。
    /// 慢走节奏（梦核感：游荡而不是奔跑）。
    /// </summary>
    public class FirstPersonController : MonoBehaviour
    {
        [Header("移动")]
        public float walkSpeed = 1.6f;
        [Header("视角")]
        public float mouseSensitivity = 2f;
        [Range(-89f, 89f)] public float pitchLimit = 80f;
        public Transform cameraTransform; // 子相机（Bootstrap3D 绑定）

        [Header("音效（Bootstrap3D 赋值；夹具里为 null 即静音跑）")]
        public AudioClip footstepClip;
        public float stepDistance = 1.5f; // 每走这么多米落一步，按位移不按时间：慢走节奏下步频才不飘

        CharacterController cc;
        float pitch;
        float vy;
        float stepTravel;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cc == null) cc = gameObject.AddComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            // 出货包没有暂停菜单，而 Start() 就把光标锁了：不给这一条，玩家只能 Alt+F4 才能走。
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();

            // 视角：yaw 转根，pitch 转子相机
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, -pitchLimit, pitchLimit);
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // 移动
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 move = transform.forward * v + transform.right * h;
            move = Vector3.ClampMagnitude(move, 1f) * walkSpeed;

            if (cc.isGrounded) vy = 0f;
            else vy += Physics.gravity.y * Time.deltaTime;
            move.y = vy;
            cc.Move(move * Time.deltaTime);

            // 落地才计数，跳跃/下落过程中的水平位移不算步（vy 已并进 move，这里只看 xz）
            if (cc.isGrounded && footstepClip != null)
            {
                stepTravel += new Vector3(move.x, 0f, move.z).magnitude * Time.deltaTime;
                if (stepTravel >= stepDistance)
                {
                    stepTravel = 0f;
                    AudioSource.PlayClipAtPoint(footstepClip, transform.position);
                }
            }
        }
    }
}
