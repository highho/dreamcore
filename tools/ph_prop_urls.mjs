// 从 Poly Haven 的 /files/<id> 响应里抽三件下载直链：几何 fbx(1k) + 基色 jpg(1k) + 法线 jpg(1k)。
// 只认 jpg 法线：Unity 侧的 NormalMap 导入器门只吃 Normal.jpg，exr 一律判缺（不是我们没有 exr，是不要）。
// 输出每行 "<key> <size> <url>"，供 tools/fetch_props.sh 逐行读。
//
// 第 3 个参数＝材质前缀。单材质资产用大写裸名（Diffuse / nor_gl），多材质资产按材质分小写前缀
// （实测 korean_fire_extinguisher_01 是 body_/glass_/paper_ 三套，裸名 branch 根本不存在）。
// 本工程每件道具只有一份材质（Bootstrap3D 装配时 new），所以多材质资产只能取占比最大的那一套，
// 由调用方在这里点名——剩下的小材质会跟着贴同一张图，远景不可见，不为它拆材质。
import fs from 'fs';
const j = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const p = process.argv[3] || '';
const branch = (solo, multi) => (p ? p + '_' + multi : solo);
const pick = (b, ext) => {
  const t = (j[b] || {})['1k'];
  const e = t && t[ext];
  return e && e.url ? e : null;
};
const rows = [
  ['geo', pick('fbx', 'fbx')],
  ['diff', pick(branch('Diffuse', 'diff'), 'jpg')],
  ['nrm', pick(branch('nor_gl', 'nor_gl'), 'jpg')],
];
let missing = 0;
for (const [k, v] of rows) {
  if (!v) { console.log(k + ' 0 MISSING'); missing++; continue; }
  console.log(k + ' ' + v.size + ' ' + v.url);
}
process.exit(missing ? 3 : 0);
