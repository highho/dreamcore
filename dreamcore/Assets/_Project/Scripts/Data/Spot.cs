using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamcore
{
    /// <summary>
    /// 机位：一个场景位置。normal = 正常画面；anomalies = 异常变体（每张一个异常）。
    /// 由 Bootstrap 编辑器脚本从 Assets/Content/Spots/ 目录约定自动生成。
    /// </summary>
    [Serializable]
    public class Spot
    {
        public string id;
        public string displayName;
        public Sprite normal;
        public List<Sprite> anomalies = new List<Sprite>();
        public AudioClip anomalyCue; // 异常激活时的声音提示（可选）
    }

    [CreateAssetMenu(menuName = "Dreamcore/SpotsCatalog")]
    public class SpotsCatalog : ScriptableObject
    {
        public List<Spot> spots = new List<Spot>();
    }
}
