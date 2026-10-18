#!/bin/sh
# ============================================================
# 一键出货：Bootstrap3D（重建 Main3D 场景）→ 构建入口切到 Main3D → Windows x64 构建 → zip
# 用法: ./tools/build.sh
# 2D 保底轨道已于 2026-10-17 由用户裁定废弃（"2d的可以扔了"），本脚本不再有模式分支。
# ============================================================
set -eu

JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="/e/Unity/Unity/Editor/Editor/Unity.exe"
PROJ="$JAM_ROOT/dreamcore"
OUT="$JAM_ROOT/builds/win64-3d"
NAME="Dreamcore"
mkdir -p "$OUT" "$JAM_ROOT/logs"

echo "[1/3] Bootstrap3D（重建 Main3D 场景 + 收养 FBX，幂等）..."
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run \
  -logFile "$JAM_ROOT/logs/bootstrap3d.log"
if grep -q "error CS" "$JAM_ROOT/logs/bootstrap3d.log"; then
  echo "!! 编译错误，见 logs/bootstrap3d.log"; exit 1
fi

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Dreamcore.EditorTools.Bootstrap3D.Activate3D \
  -logFile "$JAM_ROOT/logs/activate3d.log"
if grep -q "error CS" "$JAM_ROOT/logs/activate3d.log"; then
  echo "!! 切换构建入口失败，见 logs/activate3d.log"; exit 1
fi

echo "[2/3] 构建 Windows x64（脚本后端 Mono，产物里有 Dreamcore_Data/Managed/*.dll 为证）..."
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
