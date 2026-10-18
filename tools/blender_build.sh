#!/bin/sh
# ============================================================
# M2 建模段胶水：headless 跑 blender/build_scene.py（建模 + 导出 Corridor.fbx），
# 断言产物在位且 Blender 日志干净，再跑 Unity batchmode 的 Verify3D.VerifyFbx 并 grep 验收行。
# 退出码约定：0 = FBX 落盘 且 Blender 日志无 Error 且 日志里的 [VERIFY] 结论符合预期。
#   默认期望 PASS；VERIFY_BAD=1 时把期望翻成 FAIL（坏样本回喂，证明门是活的）。
# 用法: ./tools/blender_build.sh
# ============================================================
set -eu
JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BLENDER="${BLENDER_EXE:-/d/Game Library/Steam/steamapps/common/Blender/blender.exe}"
UNITY="/e/Unity/Unity/Editor/Editor/Unity.exe"
PROJ="$JAM_ROOT/dreamcore"
FBX="$PROJ/Assets/Content/3D/Corridor.fbx"
BODY_FBX="$PROJ/Assets/Content/3D/Body.fbx"
BLOG="$JAM_ROOT/logs/blender-build.log"
VLOG="$JAM_ROOT/logs/verify-fbx.log"
mkdir -p "$JAM_ROOT/logs"
EXPECT=PASS
[ "${VERIFY_BAD:-0}" = "1" ] && { EXPECT=FAIL; VLOG="$JAM_ROOT/logs/verify-fbx-bad.log"; }

echo "[1/2] Blender headless 建模 + 导出 FBX..."
# --python-exit-code 4 是必需的：没有它，脚本里的 Python 异常照样 exit 0，门就废了。
# 实测（Blender 5.2.2）：该参数必须放在 --python 之**前**，放后面会被忽略（rc 仍为 0）。
"$BLENDER" --background --python-exit-code 4 --python "$JAM_ROOT/blender/build_scene.py" \
  > "$BLOG" 2>&1 \
  || { echo "!! Blender 退出码非 0（Python 异常，见 $BLOG）"; exit 1; }
grep -E '\[BUILD\]|Error' "$BLOG" || true
grep -q '\[BUILD\] objects=' "$BLOG" \
  || { echo "!! 建模脚本没打印 objects=（Python 异常？见 $BLOG）"; exit 1; }
grep -q '\[BUILD\] wire_check=ok' "$BLOG" \
  || { echo "!! wire 契约自检没过（名字不是前缀式语法），见 $BLOG"; exit 1; }
grep -q "^\[BUILD\] anom_ids=.*'10'" "$BLOG" \
  || { echo "!! 10 组异常没造全，见 $BLOG"; exit 1; }
# 腿脚段（M7）：四件、名字齐、Body.fbx 落盘。身体件走的是另一棵树，
# Unity 侧的门管不到"Blender 有没有把它造出来"，所以这里就得判红。
grep -q '^\[BUILD\] body_parts=4 ' "$BLOG" \
  || { echo "!! Body.fbx 不是四件腿脚（或 build_scene 没跑第二段），见 $BLOG"; exit 1; }
grep -q "\[BUILD\] body_names=\['Leg_L', 'Leg_R', 'Shoe_L', 'Shoe_R'\]" "$BLOG" \
  || { echo "!! 腿脚名字不齐或多了别的东西，见 $BLOG 的 body_names 行"; exit 1; }
[ -f "$FBX" ] || { echo "!! 缺产物：$FBX"; exit 1; }
[ -f "$BODY_FBX" ] || { echo "!! 缺产物：$BODY_FBX"; exit 1; }
# 日志干净门：Blender 侧任何 Error:（含 "Error: Not found: bpy.ops.export_scene.fbx"
# = 导出插件未启用）都判红，不能只靠退出码 —— 退出码管不到非致命报错。
grep -E 'Error:' "$BLOG" > "$JAM_ROOT/logs/.blender-build-errors.txt" \
  && { echo "!! Blender 日志含 Error:，见 $BLOG"; cat "$JAM_ROOT/logs/.blender-build-errors.txt"; exit 1; } || true
rm -f "$JAM_ROOT/logs/.blender-build-errors.txt"
echo "== FBX OK: $FBX ($(wc -c < "$FBX") bytes) =="

echo "[2/2] Unity batchmode FBX 层级断言（期望 $EXPECT）..."
# 防 stale-log 假绿：Unity 若在打开日志前就中止（许可证抖动、工程锁、崩溃），上一轮的
# [VERIFY] PASS 会留在 $VLOG 里被本门误读成 PASS。故：跑之前删日志、立一个时间戳标记，
# 跑之后强制要求日志存在且比标记新 —— 三者缺一律判红退出，绝不认陈旧日志。
MARK="$JAM_ROOT/logs/.fbx-marker"
rm -f "$VLOG" "$MARK"
touch "$MARK"
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Dreamcore.EditorTools.Verify3D.VerifyFbx \
  -logFile "$VLOG" || true
[ -f "$VLOG" ] || { echo "!! Unity 没写日志 $VLOG（可能在开日志前中止）"; exit 1; }
[ "$VLOG" -nt "$MARK" ] || { echo "!! 日志不比本轮标记新，疑似陈旧/未刷新（拒绝据此判绿）"; exit 1; }
grep -E '\[VERIFY\]|error CS' "$VLOG" | head -40 || true
grep -q 'error CS' "$VLOG" \
  && { echo "!! Verify3D 编译失败，见 $VLOG"; exit 1; }
# 解析器警告必须为空响：出现即 FBX 侧 *_off 与 base 名字对不上，逐条改名重导。
grep -q '指认不到 base 目标' "$VLOG" \
  && { echo "!! 有 hide 代理指认不到 base 目标（名字不匹配），见 $VLOG 的 [Resolver] 行"; exit 1; } || true
line=$(grep -oE '\[VERIFY\] (PASS|FAIL)[^"]*' "$VLOG" | tail -1 || true)
case "$line" in
  *"VERIFY] $EXPECT"*) echo "== 符合预期 $EXPECT :: $line ==" ;;
  *) echo "!! 期望 $EXPECT，实际这一行：$line"; exit 1 ;;
esac
