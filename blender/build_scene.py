# blender/build_scene.py
# 总入口：重建工厂设置 → base + anom → 存 .blend → 导出 Corridor.fbx → 清场重导 Body.fbx。
# 两份 FBX 分文件：走廊那棵树的名字集合是按名验收的契约面，玩家身体不能混进去（见 gen_body.py）。
# 用法（headless，见 tools/blender_build.sh）:
#   blender --background --python-exit-code 4 --python blender/build_scene.py
# --python-exit-code 4 必须放在 --python 之前（实测放后面会被忽略，异常照样退 0）。
import os
import re
import sys
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_corridor
import gen_anomalies
import gen_props
import gen_body

BLEND_OUT = os.environ.get("CORRIDOR_BLEND", os.path.join(HERE, "Corridor.blend"))
FBX_OUT = os.environ.get(
    "CORRIDOR_FBX_OUT",
    r"G:\AIgame\jam\dreamcore\Assets\Content\3D\Corridor.fbx")
# 腿脚单独一个 FBX：走廊那棵树的名字集合被两道按名管着的门守着（逐字保真 / 未登记名必须为 0），
# 身体件混进去只会被当成漏登记的杂物判红。契约细节见 gen_body.py 头部。
BODY_FBX_OUT = os.environ.get(
    "BODY_FBX_OUT",
    r"G:\AIgame\jam\dreamcore\Assets\Content\3D\Body.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene_coll = bpy.context.scene.collection

base_coll = gen_corridor.build(scene_coll)
gen_anomalies.build(scene_coll, gen_corridor.LIGHT_ZS)
# 杂物并入 base 集合（在 wire 自检之前：Prop_ 名字也进 all_names，将来被 hide 指认得到）
prop_lines = gen_props.build(base_coll)

# ---- 导出前的契约自检（本地即可判红，不必等 Unity 的 [Resolver] 警告）----
# 前缀式 wire 语法必须与 AnomalyResolver.cs 的 WireGrammar 一致；这里只是发送前校验，
# 唯一真相仍在 AnomalyResolver（Task 7/8 一律调 IsAnomalyWireName）。
WIRE = re.compile(r"^anom(\d{2})_(show|hide)_(.+)$")
all_names = {o.name for o in bpy.data.objects}
wire_objs = [o for o in bpy.data.objects if o.name.startswith("anom")]
malformed = [o.name for o in wire_objs if not WIRE.match(o.name)]
missing_targets = []
for o in wire_objs:
    leaf = WIRE.match(o.name).group(3)
    if WIRE.match(o.name).group(2) != "hide":
        continue
    if not leaf.endswith("_off"):
        malformed.append(o.name + "（hide 缺 _off 后缀）")
        continue
    target = leaf[:-len("_off")]
    if target not in all_names:
        missing_targets.append(target)
if malformed or missing_targets:
    raise RuntimeError("wire 契约自检失败 malformed=%s hide缺目标=%s"
                       % (malformed, missing_targets))
# 原型件（_Proto_）只用于复制，留在场景里会跟着导进 Corridor.fbx，
# 被 Unity 侧"未登记 base 名"那条门判红——在这里先本地拦下。
leaked = [o.name for o in bpy.data.objects if o.name.startswith("_Proto_")]
if leaked:
    raise RuntimeError("杂物原型没清干净：%s" % leaked)

bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)


def export_fbx(path):
    """两份 FBX 共用同一组导出参数：单位/轴向/修改器一旦分叉，Corridor 与 Body 的
    世界量口径就不一致，Unity 侧那套"只有 Renderer.bounds 是米"的门会失真。"""
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        use_visible=True,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=True,
        apply_unit_scale=True,
        bake_space_transform=False,
        path_mode="COPY",
        embed_textures=False,
        add_leaf_bones=False,
        use_metadata=False,
    )


export_fbx(FBX_OUT)

names = [o.name for o in bpy.data.objects]
hide_count = sum(1 for o in wire_objs if WIRE.match(o.name).group(2) == "hide")
show_count = len(wire_objs) - hide_count
print("[BUILD] objects=%d exported=%s exists=%s" % (len(names), FBX_OUT, os.path.exists(FBX_OUT)))
print("[BUILD] base_children=%s" % [o.name for o in bpy.data.collections["base"].objects])
print("[BUILD] anom_ids=%s" % sorted(c.name for c in bpy.data.collections["anom"].children))
print("[BUILD] wire_names=%s" % sorted(o.name for o in wire_objs))
print("[BUILD] wire_check=ok show=%d hide=%d hide_targets_ok=%d malformed=0"
      % (show_count, hide_count, hide_count))
for line in prop_lines:
    print("[BUILD] prop " + line)
print("[BUILD] props=%d base_total=%d" % (len(prop_lines), len(bpy.data.collections["base"].objects)))

# ---- 腿脚段（M7）：走廊导出完再清场重建，身体件绝不进 Corridor.fbx ----
bpy.ops.wm.read_factory_settings(use_empty=True)
body_lines = gen_body.build(bpy.context.scene.collection)
os.makedirs(os.path.dirname(BODY_FBX_OUT), exist_ok=True)
export_fbx(BODY_FBX_OUT)
body_names = sorted(o.name for o in bpy.data.objects)
for line in body_lines:
    print("[BUILD] body " + line)
print("[BUILD] body_names=%s" % body_names)
print("[BUILD] body_parts=%d exported=%s exists=%s"
      % (len(body_names), BODY_FBX_OUT, os.path.exists(BODY_FBX_OUT)))
