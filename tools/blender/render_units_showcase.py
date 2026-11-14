"""Render studio showcase cards for each of the 9 units plus a full 9-unit true-scale lineup.

  blender -b --factory-startup --python-exit-code 1 --python tools/blender/render_units_showcase.py --

Outputs PNGs under TestResults/screenshots/:
  unit_<type>.png            (Attacker orange vs Defender blue pair for each of the 9 units)
  units_roster_lineup.png    (All 9 units side-by-side at 1:1 metre scale)
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_cli  # noqa: E402

UNITS = [
    ("infantry", "U_infantry_a", 1.15),
    ("cavalry", "U_cavalry_a", 1.70),
    ("ranged", "U_ranged_a", 1.15),
    ("siege", "U_siege_a", 3.15),
    ("spademen", "U_spademen_a", 1.15),
    ("fascine-carriers", "U_fascinecarriers_a", 1.70),
    ("engineers", "U_engineers_a", 1.45),
    ("pontoon-train", "U_pontoontrain_a", 2.75),
    ("crossing-marshals", "U_crossingmarshals_a", 1.25),
]

ATTACKER_COLOR = (0.92, 0.34, 0.12, 1.0)
DEFENDER_COLOR = (0.16, 0.46, 0.88, 1.0)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 16

    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    if world.node_tree is None:
        world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.76, 0.81, 0.88, 1.0)
        bg.inputs[1].default_value = 0.85
    return scene


def setup_lights(scene):
    def add_sun(name, energy, rot_deg, color=(1.0, 1.0, 1.0)):
        data = bpy.data.lights.new(name, type="SUN")
        data.energy = energy
        data.color = color
        data.angle = math.radians(8.0)
        obj = bpy.data.objects.new(name, data)
        obj.rotation_euler = tuple(math.radians(a) for a in rot_deg)
        scene.collection.objects.link(obj)
        return obj

    add_sun("KeyLight", 2.4, (42.0, -18.0, -35.0), (1.0, 0.97, 0.92))
    add_sun("FillLight", 1.0, (55.0, 20.0, 145.0), (0.85, 0.92, 1.0))
    add_sun("RimLight", 0.9, (25.0, 0.0, 175.0), (1.0, 0.96, 0.90))


def make_team_material(base_material, suffix, rgba):
    mat = base_material.copy()
    mat.name = f"M_palette_{suffix}"
    for node in mat.node_tree.nodes:
        if node.type == "RGB":
            node.outputs[0].default_value = rgba
        elif node.type == "TEX_IMAGE" and node.image is not None:
            node.image.colorspace_settings.name = "sRGB"
    return mat


def load_lod0_object(asset_name):
    blend_path = os.path.join(blender_cli.REPO_ROOT, "art", "source", f"{asset_name}.blend")
    mesh_name = f"{asset_name}_LOD0"
    with bpy.data.libraries.load(blend_path, link=False) as (data_from, data_to):
        data_to.objects = [n for n in data_from.objects if n == mesh_name]
    if not data_to.objects:
        raise RuntimeError(f"Could not find {mesh_name} in {blend_path}")
    obj = data_to.objects[0]
    obj.modifiers.clear()
    obj.parent = None
    return obj


def add_ground_disc(scene, center, radius, height=0.08):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=radius, depth=height, location=(center[0], center[1], -height * 0.5))
    disc = bpy.context.active_object
    disc.name = "Plinth"
    mat = bpy.data.materials.new("M_plinth")
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.34, 0.37, 0.42, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.85
    disc.data.materials.append(mat)
    return disc


def aim_camera(scene, objects, azimuth_deg, elevation_deg, width, height, padding=1.22):
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 45.0
    cam_data.clip_start = 0.05
    cam_data.clip_end = 500.0
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    pts = []
    deps = bpy.context.evaluated_depsgraph_get()
    for o in objects:
        mw = o.matrix_world
        for corner in o.bound_box:
            pts.append(mw @ Vector(corner))

    min_v = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    max_v = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (min_v + max_v) * 0.5

    az = math.radians(azimuth_deg)
    el = math.radians(elevation_deg)
    # Forward direction in Blender: unit faces -Y, so camera at -Y looks toward +Y.
    cam_dir = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el))).normalized()
    right = Vector((0.0, 0.0, 1.0)).cross(cam_dir).normalized()
    up = cam_dir.cross(right).normalized()

    half_w = max(abs((p - center).dot(right)) for p in pts)
    half_h = max(abs((p - center).dot(up)) for p in pts)
    aspect = width / float(height)
    fov_v = 2.0 * math.atan(cam_data.sensor_width / (2.0 * cam_data.lens * aspect))
    fov_h = 2.0 * math.atan(math.tan(fov_v * 0.5) * aspect)

    dist_v = (half_h * padding) / math.tan(fov_v * 0.5)
    dist_h = (half_w * padding) / math.tan(fov_h * 0.5)
    dist = max(dist_v, dist_h) + max((p - center).dot(cam_dir) for p in pts)

    cam.location = center + cam_dir * dist
    cam.rotation_euler = (-cam_dir).to_track_quat("-Z", "Y").to_euler()

    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100


def render_unit_card(unit_id, asset_name, spacing, out_dir):
    scene = clear_scene()
    setup_lights(scene)

    obj_a = load_lod0_object(asset_name)
    scene.collection.objects.link(obj_a)
    obj_d = obj_a.copy()
    obj_d.data = obj_a.data.copy()
    scene.collection.objects.link(obj_d)

    base_mat = obj_a.data.materials[0]
    mat_a = make_team_material(base_mat, "att", ATTACKER_COLOR)
    mat_d = make_team_material(base_mat, "def", DEFENDER_COLOR)
    obj_a.data.materials[0] = mat_a
    obj_d.data.materials[0] = mat_d

    # Show Attacker facing 3/4 toward camera (-Y rotated slightly right), Defender showing opposite side.
    obj_a.location = (-spacing * 0.55, 0.0, 0.0)
    obj_a.rotation_euler = (0.0, 0.0, math.radians(-28.0))
    obj_d.location = (spacing * 0.55, 0.0, 0.0)
    obj_d.rotation_euler = (0.0, 0.0, math.radians(38.0))

    add_ground_disc(scene, (0.0, 0.0), spacing * 1.45)
    bpy.context.view_layer.update()

    aim_camera(scene, [obj_a, obj_d], azimuth_deg=8.0, elevation_deg=18.0, width=1024, height=768, padding=1.24)
    out_path = os.path.join(out_dir, f"unit_{unit_id}.png")
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    print(f"render_units_showcase: wrote {out_path}")


def render_lineup(out_dir):
    scene = clear_scene()
    setup_lights(scene)

    objs = []
    # Arrange in 2 staggered rows (humanoids in front, mounts/engines behind or wide row at true scale).
    # One continuous readable display line sorted nicely by scale/role:
    # Back row: cavalry, siege, pontoon-train
    # Front row: infantry, ranged, spademen, fascine-carriers, engineers, crossing-marshals
    front_ids = ["infantry", "ranged", "spademen", "fascine-carriers", "engineers", "crossing-marshals"]
    back_ids = ["cavalry", "siege", "pontoon-train"]
    by_id = {u[0]: u for u in UNITS}

    mat_a = None
    mat_d = None

    def place(unit_id, pos_xyz, yaw_deg, is_defender):
        nonlocal mat_a, mat_d
        _, asset_name, _ = by_id[unit_id]
        obj = load_lod0_object(asset_name)
        scene.collection.objects.link(obj)
        if mat_a is None:
            base_mat = obj.data.materials[0]
            mat_a = make_team_material(base_mat, "att_lineup", ATTACKER_COLOR)
            mat_d = make_team_material(base_mat, "def_lineup", DEFENDER_COLOR)
        obj.data.materials[0] = mat_d if is_defender else mat_a
        obj.location = pos_xyz
        obj.rotation_euler = (0.0, 0.0, math.radians(yaw_deg))
        objs.append(obj)

    for i, uid in enumerate(front_ids):
        x = (i - (len(front_ids) - 1) * 0.5) * 1.65
        is_def = uid in ("spademen", "engineers", "ranged")
        place(uid, (x, -1.65, 0.0), -18.0 if i % 2 == 0 else 18.0, is_def)

    for i, uid in enumerate(back_ids):
        x = (i - (len(back_ids) - 1) * 0.5) * 3.40
        is_def = uid == "pontoon-train"
        place(uid, (x, 1.85, 0.0), -22.0 if i != 1 else 22.0, is_def)

    bpy.ops.mesh.primitive_plane_add(size=18.0, location=(0.0, 0.1, 0.0))
    ground = bpy.context.active_object
    mat = bpy.data.materials.new("M_ground")
    if mat.node_tree is None:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.36, 0.42, 0.34, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.92
    ground.data.materials.append(mat)
    bpy.context.view_layer.update()

    aim_camera(scene, objs, azimuth_deg=0.0, elevation_deg=17.0, width=1920, height=900, padding=1.18)
    out_path = os.path.join(out_dir, "units_roster_lineup.png")
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    print(f"render_units_showcase: wrote {out_path}")


def main():
    out_dir = os.path.join(blender_cli.REPO_ROOT, "TestResults", "screenshots")
    os.makedirs(out_dir, exist_ok=True)
    for unit_id, asset_name, spacing in UNITS:
        render_unit_card(unit_id, asset_name, spacing, out_dir)
    render_lineup(out_dir)


if __name__ == "__main__":
    main()
