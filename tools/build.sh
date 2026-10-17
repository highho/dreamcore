#!/bin/sh
# ============================================================
# 一键出货：Bootstrap（重建 Catalog/场景）→ Windows x64 构建 → zip
# 用法: ./tools/build.sh [2d|3d]    （默认 2d；3d 走 Main3D 入口）
# ============================================================
set -eu

MODE="${1:-2d}"
JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="/e/Unity/Unity/Editor/Editor/Unity.exe"
PROJ="$JAM_ROOT/dreamcore"
OUT="$JAM_ROOT/builds/win64"
NAME="Dreamcore"
mkdir -p "$OUT" "$JAM_ROOT/logs"

echo "[1/3] Bootstrap（重建 Catalog/场景，幂等；模式=$MODE）..."
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Dreamcore.EditorTools.Bootstrap.Run \
  -logFile "$JAM_ROOT/logs/bootstrap-final.log"
if grep -q "error CS" "$JAM_ROOT/logs/bootstrap-final.log"; then
  echo "!! 编译错误，见 logs/bootstrap-final.log"; exit 1
fi

if [ "$MODE" = "3d" ]; then
  "$UNITY" -batchmode -quit -projectPath "$PROJ" \
    -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run \
    -logFile "$JAM_ROOT/logs/bootstrap3d.log"
  grep -q "error CS" "$JAM_ROOT/logs/bootstrap3d.log" && { echo "!! 3D 编译错误"; exit 1; }
  "$UNITY" -batchmode -quit -projectPath "$PROJ" \
    -executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate3D \
    -logFile "$JAM_ROOT/logs/bootstrap3d.log"
else
  "$UNITY" -batchmode -quit -projectPath "$PROJ" \
    -executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate2D \
    -logFile "$JAM_ROOT/logs/bootstrap-final.log"
fi

echo "[2/3] 构建 Windows x64（IL2CPP）..."
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -buildWindows64Player "$OUT/$NAME.exe" \
  -logFile "$JAM_ROOT/logs/build-final.log"
if grep -q "error CS" "$JAM_ROOT/logs/build-final.log"; then
  echo "!! 构建期编译错误，见 logs/build-final.log"; exit 1
fi
[ -f "$OUT/$NAME.exe" ] || { echo "!! 构建失败：无 exe 产物"; exit 1; }

echo "[3/3] 打包 zip..."
powershell.exe -NoProfile -ExecutionPolicy Bypass -File \
  "$JAM_ROOT/tools/zip_release.ps1" -BuildDir "$OUT" -Name "$NAME"
[ -f "$OUT/$NAME-win64.zip" ] || { echo "!! zip 打包失败，检查 zip_release.ps1"; exit 1; }

echo "== 完成，提交物：$OUT/$NAME-win64.zip =="
