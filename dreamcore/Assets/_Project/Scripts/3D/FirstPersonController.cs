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

        CharacterController cc;
        float pitch;
        float vy;

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
        }
    }
}
