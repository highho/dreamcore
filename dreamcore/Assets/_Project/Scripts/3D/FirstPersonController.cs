using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 第一人称控制器（legacy input，零新包依赖）。
    /// 挂在 Player 根物体（含 CharacterController）；子相机做 pitch，根做 yaw。
    /// Player 根＝脚底（胶囊 center 抬到半高，见 Bootstrap3D），所以眼睛高度＝相机 localPosition.y。
    /// 慢走节奏（梦核感：游荡而不是奔跑）。可见腿脚与脚步声共用同一个"按位移推进"的相位。
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

        [Header("腿脚（Bootstrap3D 绑定 Body.fbx 的两条腿；为 null 即无腿，摆动整段跳过）")]
        public Transform leftLeg;
        public Transform rightLeg;
        public float legSwingDegrees = 12f;

        CharacterController cc;
        float pitch;
        float vy;
        float stride;        // 累计的地面位移（米），不清零：步序号 = floor(stride/stepDistance)
        int lastStep;
        float legBlend;      // 摆幅 0..1：停步时阻尼回 0，否则腿会僵在半路的弧度上
        Quaternion leftLegBase, rightLegBase;   // 导入后的初始 local 旋转，摆动在它之上叠加

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cc == null) cc = gameObject.AddComponent<CharacterController>();
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }
            // 基准旋转取装配存下来的那个：FBX 节点的原点姿态不是单位四元数，
            // 拿单位阵当"腿竖直"会让腿一开局就歪。
            if (leftLeg != null) leftLegBase = leftLeg.localRotation;
            if (rightLeg != null) rightLegBase = rightLeg.localRotation;
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
            float dxz = new Vector3(move.x, 0f, move.z).magnitude * Time.deltaTime;
            if (cc.isGrounded) stride += dxz;
            int step = Mathf.FloorToInt(stride / stepDistance);
            if (step != lastStep)
            {
                lastStep = step;
                if (footstepClip != null) AudioSource.PlayClipAtPoint(footstepClip, transform.position);
            }

            // 腿：与脚步声同一个相位源（stride），左右按步序号交替。
            // 一步之内这条腿走完 sin 的半个拱（向前摆到位再落回），落地的两端正好是角度 0＝腿竖直，
            // 所以脚步声和落脚对得上。摆幅按停走阻尼：不是按时间推进的话，站着不动腿会僵在半路的弧度上。
            legBlend = Mathf.MoveTowards(legBlend, dxz > 0.0001f ? 1f : 0f, Time.deltaTime * 5f);
            float t = Mathf.Clamp01(stride / stepDistance - step);
            float swing = Mathf.Sin(Mathf.PI * t) * legSwingDegrees * legBlend * (step % 2 == 0 ? 1f : -1f);
            SwingLeg(leftLeg, leftLegBase, swing);
            SwingLeg(rightLeg, rightLegBase, -swing);
        }

        /// <summary>
        /// 绕髋轴摆一条腿。轴用**父级空间**的玩家右向现算：Body 那棵树带着 FbxYaw 和导入器的
        /// 轴向翻转，腿节点的局部轴不可读（spec §9.2 同一类坑），而世界右向经过父级反变换后
        /// 在这条腿的父级空间里是个常量——玩家的 yaw 在分子分母同时出现，正好抵消。
        /// </summary>
        void SwingLeg(Transform leg, Quaternion baseLocal, float angle)
        {
            if (leg == null) return;
            Vector3 axis = leg.parent != null
                ? leg.parent.InverseTransformDirection(transform.right)
                : transform.right;
            leg.localRotation = Quaternion.AngleAxis(angle, axis) * baseLocal;
        }
    }
}
