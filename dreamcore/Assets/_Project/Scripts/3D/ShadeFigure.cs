using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 黑影贴片：始终面向玩家的纯黑 Quad（Billboard），异常表现组件。
    /// 用纯黑几何规避"AI 生图无真 alpha"的坑；黑剪影本身就是梦核美学。
    /// </summary>
    public class ShadeFigure : MonoBehaviour
    {
        public Transform lookTarget;
        public float height = 1.8f;
        public float width = 0.8f;

        /// <summary>
        /// 显隐权属于 AnomalyEntry（它用 GameObject.SetActive 开合 show/hide），
        /// 本组件只负责"始终面向玩家"，绝不去禁用自家的 MeshRenderer——
        /// 组件级 enabled 与 GameObject 级 SetActive 是两套开关，混用会让黑影异常永久不可见。
        /// </summary>
        void Awake()
        {
            if (lookTarget == null)
            {
                Camera cam = Camera.main;
                if (cam != null) lookTarget = cam.transform;
            }
        }

        void Update()
        {
            if (lookTarget == null) return;
            Vector3 look = lookTarget.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
