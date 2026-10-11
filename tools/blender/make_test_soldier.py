"""Build the M0 test soldier: a capsule in three LODs on the humanoid rig, palette UVs, a team-colour mask and a walk clip.

  blender -b --factory-startup --python tools/blender/make_test_soldier.py -- --out art/source/U_testsoldier_a.blend

It exercises the whole pipeline (names, LOD collections, palette cells, TeamMask, rig, clip) before real art exists.
The script is the source of truth for the test soldier; regenerate the .blend rather than editing it.
"""
import argparse
import math
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_cli  # noqa: E402

ASSET = "U_testsoldier_a"
RIG = "RIG_humanoid"
CLIP = "A_humanoid_walk"
FPS = 30
HEIGHT = 1.8
RADIUS = 0.25
HEAD_Z = 1.5  # above this: skin
TABARD_Z = (0.85, 1.5)  # between these: team colour

# (name, head, tail, parent): a minimal humanoid chain, Z-up, metres.
BONES = [
    ("root", (0, 0, 0), (0, 0.3, 0), None),
    ("hips", (0, 0, 0.95), (0, 0, 1.1), "root"),
    ("spine", (0, 0, 1.1), (0, 0, 1.3), "hips"),
    ("chest", (0, 0, 1.3), (0, 0, 1.5), "spine"),
    ("head", (0, 0, 1.5), (0, 0, 1.8), "chest"),
    ("upper_arm.L", (0.25, 0, 1.45), (0.25, 0, 1.15), "chest"),
    ("forearm.L", (0.25, 0, 1.15), (0.25, 0, 0.9), "upper_arm.L"),
    ("upper_arm.R", (-0.25, 0, 1.45), (-0.25, 0, 1.15), "chest"),
    ("forearm.R", (-0.25, 0, 1.15), (-0.25, 0, 0.9), "upper_arm.R"),
    ("thigh.L", (0.1, 0, 0.95), (0.1, 0, 0.5), "hips"),
    ("shin.L", (0.1, 0, 0.5), (0.1, 0, 0.05), "thigh.L"),
    ("thigh.R", (-0.1, 0, 0.95), (-0.1, 0, 0.5), "hips"),
    ("shin.R", (-0.1, 0, 0.5), (-0.1, 0, 0.05), "thigh.R"),
]

LODS = {"LOD0": (16, 12), "LOD1": (10, 8), "LOD2": (6, 4)}  # (segments, rings)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def make_rig(scene):
    armature = bpy.data.armatures.new(RIG)
    rig = bpy.data.objects.new(RIG, armature)
    scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in BONES:
        bone = armature.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def palette_uv(cell, grid):
    x, y = cell
    return ((x + 0.5) / grid, (y + 0.5) / grid)


def bone_for(co):
    x, _, z = co
    if z >= HEAD_Z:
        return "head"
    if z >= 1.3:
        return "chest"
    if z >= 0.95:
        return "spine" if z >= 1.1 else "hips"
    side = "L" if x >= 0 else "R"
    return f"thigh.{side}" if z >= 0.5 else f"shin.{side}"


def make_capsule(name, segments, rings, palette, material, rig, collection):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=RADIUS)
    stretch = HEIGHT - 2 * RADIUS
    for vert in bm.verts:
        if vert.co.z > 1e-6:
            vert.co.z += stretch
        vert.co.z += RADIUS

    uv_layer = bm.loops.layers.uv.new("UVMap")
    mask = bm.loops.layers.color.new("TeamMask")
    grid, cells = palette["grid"], palette["cells"]
    for face in bm.faces:
        z = face.calc_center_median().z
        if z >= HEAD_Z:
            cell = cells["skin"]
        elif TABARD_Z[0] <= z < TABARD_Z[1]:
            cell = cells["team_tabard"]
        else:
            cell = cells["cloth_brown"]
        team = 1.0 if cell == cells["team_tabard"] else 0.0
        for loop in face.loops:
            loop[uv_layer].uv = palette_uv(cell, grid)
            loop[mask] = (team, team, team, 1.0)

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material)

    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for bone_name, *_ in BONES:
        obj.vertex_groups.new(name=bone_name)
    for vert in mesh.vertices:
        obj.vertex_groups[bone_for(vert.co)].add([vert.index], 1.0, "REPLACE")
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    obj.parent = rig
    return obj


def make_walk(rig, scene):
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    frames = FPS  # a one-second loop
    for frame in range(1, frames + 2, 5):
        phase = 2 * math.pi * (frame - 1) / frames
        swing = math.radians(25) * math.sin(phase)
        keys = {
            "thigh.L": swing, "thigh.R": -swing,
            "shin.L": max(0.0, -swing) * 1.2, "shin.R": max(0.0, swing) * 1.2,
            "upper_arm.L": -swing * 0.6, "upper_arm.R": swing * 0.6,
        }
        for bone_name, angle in keys.items():
            pose_bone = rig.pose.bones[bone_name]
            pose_bone.rotation_euler = (angle, 0.0, 0.0)
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame)
        hips = rig.pose.bones["hips"]
        hips.location = (0.0, 0.0, 0.03 * abs(math.sin(phase)))
        hips.keyframe_insert(data_path="location", frame=frame)
    bpy.ops.object.mode_set(mode="OBJECT")

    action = rig.animation_data.action
    action.name = CLIP
    action.use_fake_user = True
    scene.frame_start, scene.frame_end = 1, frames + 1
    for pose_bone in rig.pose.bones:  # leave the rig in its rest pose
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
    return action


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", default=os.path.join(blender_cli.REPO_ROOT, "art", "source", f"{ASSET}.blend"))
    args = blender_cli.parse_args(parser)

    palette = blender_cli.load_json("art/palettes/palette.json")
    scene = reset_scene()
    rig = make_rig(scene)

    material = bpy.data.materials.new("M_palette")
    material.diffuse_color = (0.6, 0.5, 0.4, 1.0)

    for lod, (segments, rings) in LODS.items():
        collection = bpy.data.collections.new(lod)
        scene.collection.children.link(collection)
        make_capsule(f"{ASSET}_{lod}", segments, rings, palette, material, rig, collection)

    make_walk(rig, scene)
    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out, compress=True)
    print(f"make_test_soldier: wrote {out}")


if __name__ == "__main__":
    main()
