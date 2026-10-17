using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 走廊两端的判定门：玩家走进尽头门=前进判定，走回起点门=回头判定。
    /// </summary>
    public class TriggerGate : MonoBehaviour
    {
        public bool isForwardGate = true; // true=尽头（前进）/ false=起点（回头）

        Loop3D loop;

        void Awake()
        {
            loop = FindFirstObjectByType<Loop3D>();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && loop != null)
                loop.Report(isForwardGate);
        }
    }
}
