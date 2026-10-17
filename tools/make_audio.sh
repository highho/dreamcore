#!/bin/sh
# ============================================================
# 梦核音频合成：纯 ffmpeg 程序化生成，零版权风险
# 用法: ./tools/make_audio.sh
# 输出: dreamcore/Assets/_Project/Audio/*.wav（Unity 自动导入为 AudioClip）
# ============================================================
set -eu

JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$JAM_ROOT/dreamcore/Assets/_Project/Audio"
mkdir -p "$OUT"
command -v ffmpeg >/dev/null || { echo "缺少 ffmpeg"; exit 1; }

# 1) 主循环 ambience：50Hz 市电嗡鸣 + 粉噪 + 磁带底噪（低熵、循环无缝感好）
ffmpeg -y -loglevel error \
  -f lavfi -i "sine=frequency=50:duration=60" \
  -f lavfi -i "anoisesrc=d=60:c=pink:r=44100:a=0.6" \
  -filter_complex "[1:a]highpass=f=80,lowpass=f=1500,volume=0.25[bg];[0:a]volume=0.10[hum];[bg][hum]amix=inputs=2:duration=longest:normalize=0,alimiter=limit=0.6" \
  "$OUT/ambient_loop.wav"
echo "  → ambient_loop.wav"

# 2) 磁带咔哒（换带/开关声，事件音）
ffmpeg -y -loglevel error \
  -f lavfi -i "anoisesrc=d=1:c=white:r=44100:a=0.8" \
  -af "highpass=f=2000,volume=0.5,afade=t=out:st=0.3:d=0.7" \
  "$OUT/tape_click.wav"
echo "  → tape_click.wav"

# 3) 低频闷响（异常被发现的 cue）
ffmpeg -y -loglevel error \
  -f lavfi -i "sine=frequency=45:duration=2" \
  -af "volume=0.35,afade=t=in:st=0:d=0.05,afade=t=out:st=1.2:d=0.8" \
  "$OUT/anomaly_cue.wav"
echo "  → anomaly_cue.wav"

# 4) 脚步声（低频闷响短促版，机位切换反馈）
ffmpeg -y -loglevel error \
  -f lavfi -i "anoisesrc=d=0.3:c=brown:r=44100:a=0.9" \
  -af "lowpass=f=400,volume=0.5,afade=t=out:st=0.1:d=0.2" \
  "$OUT/footstep.wav"
echo "  → footstep.wav"

echo "完成。Unity 打开后自动导入；Bootstrap 已把 ambient_loop 挂到 AudioController。"
