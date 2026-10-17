using UnityEngine;

namespace Dreamcore
{
    /// <summary>
    /// 环境音循环：挂在 GameManager 上，循环播放 ambience。
    /// 事件音（脚步/异常 cue）在 LoopController 需要时经 GameManager 播放。
    /// </summary>
    public class AudioController : MonoBehaviour
    {
        [Header("环境音（Bootstrap 自动挂 ambient_loop.wav）")]
        public AudioClip ambientLoop;
        public AudioClip footstep;
        public AudioClip anomalyCue;
        [Range(0f, 1f)] public float ambientVolume = 0.55f;
        [Range(0f, 1f)] public float sfxVolume = 0.7f;

        AudioSource ambientSource;
        AudioSource sfxSource;

        void Awake()
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.clip = ambientLoop;
            ambientSource.volume = ambientVolume;
            ambientSource.playOnAwake = false;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = sfxVolume;

            if (ambientLoop != null) ambientSource.Play();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip);
        }

        public void PlayFootstep() { PlaySfx(footstep); }
        public void PlayAnomalyCue() { PlaySfx(anomalyCue); }
    }
}
