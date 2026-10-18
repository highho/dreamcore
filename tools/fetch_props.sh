#!/bin/sh
# ============================================================
# 取免费道具模型：Poly Haven（CC0）→ 本工程
# 用法: ./tools/fetch_props.sh
#   几何 fbx(1k) → ${TMPDIR:-/tmp}/ph-props/<slug>.fbx   （Blender 建模段消费，不进版本库）
#   贴图 jpg(1k) → dreamcore/Assets/Content/Textures/Prop_<slug>/{BaseColor,Normal}.jpg
#
# 为什么只取几何＋两张贴图：本工程的材质一律由 Bootstrap3D 在装配时 new 出来
# （工程内没有 .mat 资产），FBX 自带的材质引用会指向不存在的图，留着只会误导。
# 为什么法线只认 jpg：Verify3D 的导入器门只查 Normal.jpg；nor_gl 的 exr 变体一概不用。
#
# 授权红线（用户裁定）：**只允许 CC0**。每件都在下载前先抓它的资产页，
# 页面必须出现 "CC0 1.0 Universal"，否则当场红、跳过该件不取。
# ============================================================
set -eu

JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$JAM_ROOT/dreamcore/Assets/Content/Textures"
STAGE="${TMPDIR:-/tmp}/ph-props"
API="https://api.polyhaven.com/files/"
PAGE="https://polyhaven.com/a/"
mkdir -p "$OUT" "$STAGE"

# <slug>:<polyhaven 资产 id>[:<材质前缀>]
# 材质前缀只有多材质资产才要写（实测仅灭火器：body_/glass_/paper_ 三套），单材质裸名即 Diffuse/nor_gl。
PROPS="
chair:SchoolChair_01
bench:painted_wooden_bench
shelf:steel_frame_shelves_01
table:side_table_01
container:plastic_container
crate:plastic_crate_02
sign:WetFloorSign_01
bag:trashbag
extinguisher:korean_fire_extinguisher_01:body
clock:wall_clock
alarm:fire_alarm
medbox:medical_box
plunger:plunger
"

MANIFEST="$STAGE/manifest.txt"
: > "$MANIFEST"
fail=0
for pair in $PROPS; do
  slug=${pair%%:*}
  rest=${pair#*:}
  id=${rest%%:*}
  matpfx=${rest#*:}
  # 没有第三段时 ${rest#*:} 原样返回 id；写成 if 而不是 `[ ] &&`（set -e 下后者整表返回 1）
  if [ "$matpfx" = "$id" ]; then matpfx=""; fi

  lic=$(curl -s -m 60 "$PAGE$id" | grep -c "CC0 1.0 Universal" || true)
  if [ "$lic" -lt 1 ]; then
    echo "!! $id 页面没有 CC0 1.0 Universal（授权不明），按红线跳过"
    fail=1
    continue
  fi

  fj="$STAGE/$id.files.json"
  rm -f "$fj"
  curl -s -m 60 -o "$fj" "$API$id" || true
  [ -s "$fj" ] || { echo "!! $id 取不到 files 清单"; fail=1; continue; }

  urls=$(node "$JAM_ROOT/tools/ph_prop_urls.mjs" "$fj" "$matpfx") || {
    echo "!! $id 缺 1k 的 fbx/基色 jpg/法线 jpg 任一项（前缀='$matpfx'）"; fail=1; continue; }

  geo=$(echo "$urls" | grep '^geo ' | cut -d' ' -f3)
  diff=$(echo "$urls" | grep '^diff ' | cut -d' ' -f3)
  nrm=$(echo "$urls" | grep '^nrm ' | cut -d' ' -f3)
  echo "   [$slug] geo=$(echo "$urls" | grep '^geo ' | cut -d' ' -f2)B diff=$(echo "$urls" | grep '^diff ' | cut -d' ' -f2)B nrm=$(echo "$urls" | grep '^nrm ' | cut -d' ' -f2)B"

  d="$OUT/Prop_$slug"
  rm -rf "$d"
  mkdir -p "$d"
  # 分隔符用 | 不用 :：URL 自带 https:，用 : 切会把 url 截成 "https"（实测踩过）
  for spec in "$geo|$STAGE/$slug.fbx" "$diff|$d/BaseColor.jpg" "$nrm|$d/Normal.jpg"; do
    url=${spec%%|*}
    dst=${spec#*|}
    rm -f "$dst"
    code=$(curl -s -m 120 -o "$dst" -w "%{http_code}" -L "$url")
    [ "$code" = "200" ] || { echo "!! $slug $url http=$code"; fail=1; }
    [ -s "$dst" ] || { echo "!! $slug $dst 是空文件"; fail=1; }
  done
  n=$(ls "$d" | grep -c '\.jpg$')
  [ "$n" = "2" ] || { echo "!! $slug 贴图张数=$n（期望 2）"; fail=1; }
  echo "$slug $id" >> "$MANIFEST"
  echo "[OK] $slug ($id) → Prop_$slug + $STAGE/$slug.fbx"
done

if [ "$fail" -ne 0 ]; then
  echo "!! 有道具没取全，见上方逐件原因"
  exit 1
fi

cat > "$OUT/PROPS_CREDITS.md" <<'EOF'
# 道具模型出处（全部 CC0）

来源：[Poly Haven](https://polyhaven.com/) —— 模型与贴图均以 **CC0 1.0 Universal**（公有领域）发布，
可商用、无需署名（此处署名仅为可追溯）。本文件由 `./tools/fetch_props.sh` 生成，
每件在下图前都抓过它的资产页并核到 "CC0 1.0 Universal" 字样。

| 目录 / 网格名 | Poly Haven 资产 | 本项目用途 |
|---|---|---|
| Prop_chair | SchoolChair_01 | 靠墙课椅 Prop_chair_1/2/3 |
| Prop_bench | painted_wooden_bench | 长凳 Prop_bench |
| Prop_shelf | steel_frame_shelves_01 | 金属置物架 Prop_shelf |
| Prop_table | side_table_01 | 小边桌 Prop_table |
| Prop_container | plastic_container | 塑料周转箱 Prop_container |
| Prop_crate | plastic_crate_02 | 塑料筐 Prop_crate |
| Prop_sign | WetFloorSign_01 | "小心地滑"立牌 Prop_sign |
| Prop_bag | trashbag | 垃圾袋 Prop_bag |
| Prop_extinguisher | korean_fire_extinguisher_01 | 灭火器 Prop_extinguisher |
| Prop_clock | wall_clock | 挂钟 Prop_clock |
| Prop_alarm | fire_alarm | 火警按钮 Prop_alarm |
| Prop_medbox | medical_box | 急救箱 Prop_medbox |
| Prop_plunger | plunger | 皮搋子 Prop_plunger |

每件只留 `BaseColor.jpg` + `Normal.jpg` 两张 1K 图（由 Poly Haven 的 `diff` / `nor_gl` 改名而来，
法线一律取 **jpg 变体的 OpenGL 图**）；几何 fbx(1k) 留在 `${TMPDIR:-/tmp}/ph-props/`，不进版本库
——它只被 blender/gen_props.py 消费，最终随 Corridor.fbx 一起导出。
EOF

echo "== 完成：$(wc -l < "$MANIFEST") 件道具，贴图在 $OUT/Prop_*，几何在 $STAGE =="
