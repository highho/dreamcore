#!/bin/sh
# ============================================================
# 取免费 3D 素材：ambientCG（CC0）PBR 贴图 → 本工程 Content/Textures
# 用法: ./tools/fetch_textures.sh [输出目录]
#   默认输出 dreamcore/Assets/Content/Textures（务必在没有 Unity 进程在飞时写入 Assets/）
#   想先暂存：./tools/fetch_textures.sh /tmp/acg-stage
# 只做三件事：下载 1K-JPG 包 → 只抽三张 map → 落成工程内固定文件名 → 落 CC0 出处清单
# 每个 map 都保留 1K（约 3MB/材质）；zip 本体不进版本库。
# 实测（2026-10-17）：zip 内部是扁平的 ${id}_1K-JPG_<Map>.jpg，且 ambientCG 把基色叫
# **_Color**（不是 Base_Color）；解出来一律改名成 BaseColor/Normal/Roughness，
# 让 Unity 侧的贴图名与资产 id 解耦（换资产不用改代码）。
# ============================================================
set -eu

OUT="${1:-$(cd "$(dirname "$0")/.." && pwd)/dreamcore/Assets/Content/Textures}"
STAGE="${TMPDIR:-/tmp}/acg-zips"
BASE="https://ambientcg.com/get?file="
IDS="Terrazzo002 Plaster007 Tiles032 PaintedMetal006 Wood073 Concrete002 Paper003"
# ambientCG 原名 → 工程内固定名。Unity 侧代码只认后者，换资产不用改代码。
MAPS="Color:BaseColor NormalGL:Normal Roughness:Roughness"

mkdir -p "$OUT" "$STAGE"
for id in $IDS; do
  zip="$STAGE/${id}_1K-JPG.zip"
  # 防陈旧：先删再下，下完必须存在且非空，否则当场红
  rm -f "$zip"
  code=$(curl -s -m 120 -o "$zip" -w "%{http_code}" -L "$BASE${id}_1K-JPG.zip")
  [ "$code" = "200" ] || { echo "!! $id 下载 http=$code"; exit 1; }
  [ -s "$zip" ] || { echo "!! $id 下载文件为空"; exit 1; }
  d="$OUT/$id"
  rm -rf "$d"
  mkdir -p "$d"
  for pair in $MAPS; do
    acg=${pair%%:*}
    local_name=${pair##*:}
    unzip -q -o "$zip" "${id}_1K-JPG_${acg}.jpg" -d "$d" || { echo "!! $id 缺 $acg map"; exit 1; }
    f="$d/${id}_1K-JPG_${acg}.jpg"
    [ -s "$f" ] || { echo "!! $id $acg 解出来是空文件"; exit 1; }
    mv -f "$f" "$d/$local_name.jpg"
  done
  n=$(ls "$d" | grep -c '\.jpg$')
  [ "$n" = "3" ] || { echo "!! $id 贴图张数=$n（期望 3）"; exit 1; }
  echo "[OK] $id -> $d ($n maps, $(du -sh "$d" | cut -f1))"
done

cat > "$OUT/CREDITS.md" <<'EOF'
# 贴图出处（全部 CC0）

来源：[ambientCG](https://ambientcg.com/) —— 所有素材以 **CC0 1.0 Universal** 发布，
可商用、无需署名（此处署名仅为礼貌与可追溯）。本文件由 `./tools/fetch_textures.sh` 生成。

| 目录 | ambientCG 资产 | 本项目用途 |
|---|---|---|
| Terrazzo002 | Terrazzo002 (CC0) | 水磨石地面 Floor |
| Plaster007 | Plaster007 (CC0) | 石灰白墙 WallLeft/WallRight、天花板 Ceiling、端墙 EndWall、白漆窗框/灯座 |
| Tiles032 | Tiles032 (CC0) | 绿瓷砖墙裙 WainscotL/WainscotR（资产本身就是墨绿釉面砖，不再靠 tint 染） |
| PaintedMetal006 | PaintedMetal006 (CC0) | 爆漆绿铁：尽头铁门 EndDoor、信箱 Mailbox*、门牌 DoorPlate_* |
| Wood073 | Wood073 (CC0) | 实木门板 Door_3/Door_9 与门套（带木节的整板纹，比地板拼花贴得像门） |
| Concrete002 | Concrete002 (CC0) | 哑光金属件底纹（管/栏杆/把手/线槽/分格条）、渗水、异常件底面 |
| Paper003 | Paper003 (CC0) | 墙上小广告 Poster_*（揉皱的白纸才有纸味） |

这一批是 M6 质感线换的：Plaster004（脏米色粗灰泥）、Tiles006（白砖靠 tint 染绿）、
WoodFloor051（拼花地板贴到门上）三张在实机截图里被判定"题材不对"，换成上表的资产。

每个资产只保留 `BaseColor.jpg` / `Normal.jpg` / `Roughness.jpg` 三张 1K 图（由 ambientCG 的
`Color` / `NormalGL` / `Roughness` 改名而来）；
原始 zip 留在 `${TMPDIR:-/tmp}/acg-zips`，不进版本库。
EOF

echo "== 完成：$OUT（$(( $(ls -d "$OUT"/*/ 2>/dev/null | wc -l) )) 个材质目录）=="
