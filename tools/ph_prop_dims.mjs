// 一次性探针（spike，用完即可扔）：从 Poly Haven 索引里取这 13 件道具的官方尺寸与三角数，
// 作为 blender/gen_props.py 里 want 尺寸的独立口径（不能拿自己的实测当判据）。
// 实测：索引的 dimensions 是**毫米为单位的三元数组** [x,y,z]，不是 {x,y,z} 对象。
// 用法: node tools/ph_prop_dims.mjs <index.json> <id,id,...>
import fs from 'fs';
const j = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
for (const id of process.argv[3].split(',')) {
  const a = j[id];
  if (!a) { console.log(id + ' NOT_IN_INDEX'); continue; }
  const d = a.dimensions || [];
  console.log(id + ' dims_m=[' + d.map(v => (v / 1000).toFixed(3)).join(', ') + ']' +
              ' tris=' + a.polycount);
}
