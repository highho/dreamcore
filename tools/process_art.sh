#!/bin/sh
# ============================================================
# 梦核美术降质批处理：AI 生图(art/raw) → 统一低清梦核质感 → Unity 资产目录
#
# 用法（Git Bash）:  ./tools/process_art.sh
# 输入:  art/raw/spot_XX/normal.png|jpg + anomaly_NN.png|jpg
#        建议生图选 16:9；非 16:9 会自动黑边居中到 960x540
# 输出:  dreamcore/Assets/Content/Spots/spot_XX/*.jpg（覆盖同名）
# 跑完后必须重跑 Bootstrap 重新生成 SpotsCatalog：
#   Unity.exe -batchmode -quit -projectPath dreamcore -executeMethod Dreamcore.EditorTools.Bootstrap.Run
#
# 统一降质参数（= 风格一致性的核心，改风格改这里，不要逐张手调）：
#   960x540 低清基线 / CCD 噪点 / 轻微去饱和 / 微提亮 / 高压缩 jpg 伪影
# ============================================================
set -eu

JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RAW="$JAM_ROOT/art/raw"
OUT="$JAM_ROOT/dreamcore/Assets/Content/Spots"

[ -d "$RAW" ] || { echo "缺少 $RAW —— 先放生图（spot_XX/normal.png + anomaly_NN.png）"; exit 1; }
command -v ffmpeg >/dev/null || { echo "缺少 ffmpeg"; exit 1; }
mkdir -p "$OUT"

process() {
  # $1=输入 $2=输出
  ffmpeg -y -loglevel error -i "$1" -vf \
    "scale=960:540:force_original_aspect_ratio=decrease,pad=960:540:(ow-iw)/2:(oh-ih)/2:color=black,noise=alls=14:allf=t+u,eq=saturation=0.88:contrast=1.06:gamma=0.97,format=yuv420p" \
    -q:v 13 "$2"
  echo "  → $2"
}

found=0
for d in "$RAW"/spot_*; do
  [ -d "$d" ] || continue
  name=$(basename "$d")
  mkdir -p "$OUT/$name"

  for ext in png jpg jpeg; do
    if [ -f "$d/normal.$ext" ]; then
      process "$d/normal.$ext" "$OUT/$name/normal.jpg"
      found=1
      break
    fi
  done

  for f in "$d"/anomaly_*.png "$d"/anomaly_*.jpg "$d"/anomaly_*.jpeg; do
    [ -f "$f" ] || continue
    bn=$(basename "$f"); stem="${bn%.*}"
    process "$f" "$OUT/$name/$stem.jpg"
    found=1
  done
done

[ "$found" = 1 ] || echo "!! art/raw 下没有 spot_XX 目录或图片"
echo "完成。下一步：重跑 Bootstrap 生成 Catalog。"
