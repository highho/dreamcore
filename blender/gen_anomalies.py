# blender/gen_anomalies.py
# 10 组异常变体。show 里是真几何，hide 里只放 EMPTY 名字载体。
#
# 命名契约（前缀式，见 spec §9.1 与 AnomalyResolver 的语法 ^anom(\d{2})_(show|hide)_(.+)$）：
# 集合（anom/<NN>/{show,hide}）在 Blender 侧只做编排，**实测从不进 FBX**，所以每个物件必须
# 自带完整 wire 名，Unity 侧才解析得到：
#   anom<NN>_show_<leaf>                  异常激活时出现的网格
#   anom<NN>_hide_<base目标名>_off        异常激活时消失的指认代理（剥掉 _off 即 base 里的名字）
# hide 代理自己在 Unity 侧被 AnomalyResolver 关掉，永不显示。
#
# M6 结构段的连带义务：**一个 base 件被某组异常"抹掉"，它的所有可视零件都得各有一条 hide 代理**。
# 门板与门把手是两个网格，所以 anom03 现在指认 2 个；六盏灯各多了一根自发光灯管，
# 所以 anom01（灯全灭）从 6 条涨到 12 条——漏掉灯管会得到"灯灭了、灯管还亮着"。
# 名字与位置由 build_scene.py 的导出前自检兜底：hide 指认不到 base 目标就直接抛异常。
from gen_corridor import (DOOR_ALONGS, DOOR_H, DOOR_W, FLOOR_TOP, HEIGHT, IN_Y, LIGHT_ZS,
                          OUT_Y, WIN_ALONG, WIN_W, WIN_Z0, WIN_Z1, WIDTH, box, coll, empty)

HALF_W = WIDTH * 0.5


def group(anom_root, nn):
    g = coll(nn, anom_root)
    return coll("show", g), coll("hide", g)


def build(scene_coll, light_zs):
    anom = coll("anom", scene_coll)

    s, h = group(anom, "01")            # 声控灯全灭，只剩绿色应急光
    box("anom01_show_emergencyLight", s, 12.0, 0.35, 0.0, (0.3, 0.12, 0.12))
    for z in light_zs:
        tag = int(z)
        empty("anom01_hide_Light_%d_off" % tag, h, z, HEIGHT - 0.25, 0.0)
        # 灯管是 Unlit 自发光，点光灭了它不会跟着灭，必须显式收掉（灯座留在原地）
        empty("anom01_hide_Lamp_%d_off" % tag, h, z, HEIGHT - 0.0775, 0.0)

    s, h = group(anom, "02")            # 尽头多出背对镜头的人影
    box("anom02_show_extraFigure", s, 23.0, 0.10 + 0.9, 0.0, (0.2, 0.8, 1.8))

    s, h = group(anom, "03")            # 一户防盗门半开
    a = DOOR_ALONGS[0]
    # 门板绕门套边 hinge 向走廊内摆开：hinge 在内表面 -IN_Y，板宽 DOOR_W-0.1
    hinge_y = -IN_Y
    box("anom03_show_Door_3_open", s, a + DOOR_W * 0.5, 0.5 * DOOR_H - 0.05,
        hinge_y + 0.5 * (DOOR_W - 0.1), (0.05, DOOR_W - 0.1, DOOR_H - 0.1))
    empty("anom03_hide_Door_3_off", h, a, -IN_Y - 0.015, 0.5 * DOOR_H - 0.05)
    empty("anom03_hide_Door_3_Handle_off", h, a + DOOR_W * 0.5 - 0.13, -IN_Y + 0.02, 1.0)

    s, h = group(anom, "04")            # 开锁疏通小广告整排消失
    empty("anom04_hide_Poster_6_off", h, 6.0, -IN_Y + 0.010, 1.60)
    empty("anom04_hide_Poster_10_off", h, 11.4, IN_Y - 0.010, 1.60)
    empty("anom04_hide_Poster_18_off", h, 18.0, -IN_Y + 0.010, 1.55)

    s, h = group(anom, "05")            # 地上多出一双小孩旧布鞋
    # height 必须 > 楼板上表面 FLOOR_TOP：旧值 0.04（占 z 0–0.08）整件埋在 Floor 板里，实测逐像素不可见
    box("anom05_show_Shoes", s, 7.2, FLOOR_TOP + 0.05, -0.35, (0.18, 0.42, 0.08))

    s, h = group(anom, "06")            # 靠墙的旧自行车倒了
    box("anom06_show_BikeUp_open", s, 15.5, FLOOR_TOP + 0.21, 0.62, (0.45, 1.0, 0.42))
    empty("anom06_hide_BikeUp_off", h, 15.5, 1.02, 0.6)

    s, h = group(anom, "07")            # 信报箱小门全部敞开
    # 门 hinge 在箱体前脸 0.97，向外摆开 ⇒ 板面占 y 0.32..0.97
    box("anom07_show_MailboxDoor_open", s, 12.6, 1.25, 0.645, (0.03, 0.65, 0.75))
    empty("anom07_hide_MailboxDoor_off", h, 12.6, 0.955, 1.25)
    empty("anom07_hide_Mailbox_Div1_off", h, 12.28, 0.9385, 1.25)
    empty("anom07_hide_Mailbox_Div2_off", h, 12.92, 0.9385, 1.25)

    s, h = group(anom, "08")            # 门缝下渗出一滩水
    # 水膜从门槛（内表面）向走廊内摊开，别糊进门套（门套占 y -1.15..-1.08）
    box("anom08_show_WaterPuddle", s, DOOR_ALONGS[0], FLOOR_TOP + 0.01, -0.78, (0.45, 0.66, 0.01))

    s, h = group(anom, "09")            # 天花板垂下一段红电线
    box("anom09_show_RedWire", s, 17.2, 2.45, 0.30, (0.035, 0.035, 1.1))

    s, h = group(anom, "10")            # 窗外该有楼的地方全黑
    # 黑面板压在淡蓝衬板（base 在 OUT_Y-0.01）靠走廊一侧，两者都在墙体厚度内，
    # 透过真窗洞才看得见——z 高度与 base 逐字对齐，否则"全黑"盖不住窗。
    box("anom10_show_WindowBuildings_open", s, WIN_ALONG, 0.5 * (WIN_Z0 + WIN_Z1), OUT_Y - 0.030,
        (WIN_W, 0.02, WIN_Z1 - WIN_Z0))
    empty("anom10_hide_WindowBuildings_off", h, WIN_ALONG, OUT_Y - 0.010, 0.5 * (WIN_Z0 + WIN_Z1))

    return anom
