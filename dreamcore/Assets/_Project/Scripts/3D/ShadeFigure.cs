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

        MeshRenderer mr;

        void Awake()
        {
            mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            if (lookTarget == null)
            {
                Camera cam = Camera.main;
                if (cam != null) lookTarget = cam.transform;
            }
        }

        public void SetActive(bool on)
        {
            if (mr != null) mr.enabled = on;
        }

        void Update()
        {
            if (mr == null || !mr.enabled || lookTarget == null) return;
            Vector3 look = lookTarget.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
