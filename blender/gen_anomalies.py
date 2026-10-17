# blender/gen_anomalies.py
# 10 组异常变体。show 里是真几何，hide 里只放 EMPTY 名字载体。
#
# 命名契约（前缀式，见 spec §9.1 与 AnomalyResolver 的语法 ^anom(\d{2})_(show|hide)_(.+)$）：
# 集合（anom/<NN>/{show,hide}）在 Blender 侧只做编排，**实测从不进 FBX**，所以每个物件必须
# 自带完整 wire 名，Unity 侧才解析得到：
#   anom<NN>_show_<leaf>                  异常激活时出现的网格
#   anom<NN>_hide_<base目标名>_off        异常激活时消失的指认代理（剥掉 _off 即 base 里的名字）
# hide 代理自己在 Unity 侧被 AnomalyResolver 关掉，永不显示。
# 名字集合与 Task 5 灰盒（GreyboxAnomalies.cs）逐字一致：9 个 show + 13 个 _off 代理 = 22 个引用。
import bpy
from gen_corridor import HEIGHT, WIDTH, coll, box, empty, move_to

HALF_W = WIDTH * 0.5


def group(anom_root, nn):
    g = coll(nn, anom_root)
    return coll("show", g), coll("hide", g)


def build(scene_coll, light_zs):
    anom = coll("anom", scene_coll)

    s, h = group(anom, "01")            # 声控灯全灭，只剩绿色应急光
    box("anom01_show_emergencyLight", s, 12.0, 0.35, 0.0, (0.3, 0.12, 0.12))
    for z in light_zs:
        empty("anom01_hide_Light_%d_off" % int(z), h, z, HEIGHT - 0.25, 0.0)

    s, h = group(anom, "02")            # 尽头多出背对镜头的人影
    box("anom02_show_extraFigure", s, 23.0, 0.9, 0.0, (0.2, 0.8, 1.8))

    s, h = group(anom, "03")            # 一户防盗门半开
    box("anom03_show_Door_3_open", s, 3.85, 1.05, -HALF_W + 0.45, (0.04, 0.9, 2.1))
    empty("anom03_hide_Door_3_off", h, 3.4, 1.05, -HALF_W + 0.06)

    s, h = group(anom, "04")            # 开锁疏通小广告整排消失
    empty("anom04_hide_Poster_6_off", h, 6.0, 1.5, -HALF_W + 0.07)
    empty("anom04_hide_Poster_10_off", h, 10.0, 1.5, HALF_W - 0.07)
    empty("anom04_hide_Poster_18_off", h, 18.0, 1.45, -HALF_W + 0.07)

    s, h = group(anom, "05")            # 地上多出一双小孩旧布鞋
    box("anom05_show_Shoes", s, 7.2, 0.04, -0.35, (0.18, 0.42, 0.08))

    s, h = group(anom, "06")            # 靠墙的旧自行车倒了
    box("anom06_show_BikeUp_open", s, 15.5, 0.22, HALF_W - 0.20, (0.5, 1.05, 0.42))
    empty("anom06_hide_BikeUp_off", h, 15.5, 0.55, HALF_W - 0.30)

    s, h = group(anom, "07")            # 信报箱小门全部敞开
    box("anom07_show_MailboxDoor_open", s, 12.6, 1.25, HALF_W - 0.35, (0.03, 0.65, 0.75))
    empty("anom07_hide_MailboxDoor_off", h, 12.6, 1.25, HALF_W - 0.02)

    s, h = group(anom, "08")            # 门缝下渗出一滩水
    box("anom08_show_WaterPuddle", s, 3.4, 0.02, -HALF_W + 0.35, (0.45, 0.7, 0.01))

    s, h = group(anom, "09")            # 天花板垂下一段红电线
    box("anom09_show_RedWire", s, 17.2, 2.45, 0.30, (0.035, 0.035, 1.1))

    s, h = group(anom, "10")            # 窗外该有楼的地方全黑
    box("anom10_show_WindowBuildings_open", s, 9.0, 1.9, HALF_W + 0.45, (3.4, 0.02, 1.6))
    empty("anom10_hide_WindowBuildings_off", h, 9.0, 1.9, HALF_W + 0.45)

    return anom
