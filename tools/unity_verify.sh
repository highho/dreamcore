#!/bin/sh
# 三段验收一次跑完。VERIFY_BAD=1 时把期望从 PASS 翻成 FAIL（坏样本回喂）。
#
# 防陈旧日志（假绿路径，本项目明令堵死）：Unity 如果压根没写日志（崩在启动早期、
# 单锁被占、executeMethod 名打错），grep 到的会是上一次跑剩的 PASS —— 那是"能编译、
# 验收全绿、但游戏不能玩"的经典形态。所以每个 run_one 跑前删日志 + touch 同目录 marker，
# 跑后用 `[ "$log" -nt "$marker" ]` 证明这份日志确实是本次新写的。
# 注意 test 的操作数顺序只能是 LOG -nt MARKER（三操作数写法 `test -nt MARKER LOG`
# 是无效 bash、退出码 2，会被 set -e 直接打死成假红）。
set -eu
JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="/e/Unity/Unity/Editor/Editor/Unity.exe"
PROJ="$JAM_ROOT/dreamcore"
mkdir -p "$JAM_ROOT/logs"
EXPECT=PASS
# `[ ] && EXPECT=FAIL` 在 set -eu 下安全：测试失败时列表返回非 0，但它是 && 列表的
# 非末位命令，不触发 errexit——这正是我们要的"条件赋值不致命"。
[ "${VERIFY_BAD:-0}" = "1" ] && EXPECT=FAIL

run_one() {
  method="$1"; tag="$2"
  log="$JAM_ROOT/logs/verify-$tag.log"
  marker="$JAM_ROOT/logs/verify-$tag.marker"
  rm -f "$log"
  touch "$marker"
  "$UNITY" -batchmode -quit -projectPath "$PROJ" \
    -executeMethod "Dreamcore.EditorTools.Verify3D.$method" \
    -logFile "$log" || true
  [ "$log" -nt "$marker" ] || { echo "[$tag] 日志未刷新（陈旧）"; exit 1; }
  line=$(grep -E "\[VERIFY\] (PASS|FAIL)" "$log" | tail -1 || true)
  case "$line" in
    *"VERIFY] $EXPECT"*) echo "[$tag] 符合预期 $EXPECT :: $line" ;;
    *) echo "[$tag] 期望 $EXPECT，实际这一行：$line"; exit 1 ;;
  esac
}

run_one VerifyFbx fbx
run_one VerifyAssembly asm
run_one VerifyGameplay play
echo "== 分段验收全部符合预期（EXPECT=$EXPECT）=="
# 坏样本回喂：三段都如实变红才算"门有牙"，但整次跑本质是红跑，退出码 1 让上层能区分它和绿跑。
# 用 if 而不是 `[ ] && exit 1`：后者在 EXPECT=PASS 时测试失败、又恰好是脚本末条命令，
# 会被 set -e 当成退出码 1，把绿跑也判成失败。
if [ "$EXPECT" = "FAIL" ]; then exit 1; fi
