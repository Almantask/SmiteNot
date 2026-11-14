"""Build all 9 unit visuals currently used by the simulation and write source metadata + shared palette PNG.

  blender -b --factory-startup --python tools/blender/make_units.py
  blender -b --factory-startup --python tools/blender/make_units.py -- --only infantry cavalry

Generates art/palettes/palette.png, art/source/U_<type>_a.blend and art/source/U_<type>_a.source.json
for all 9 types in data/rules/unit-types.json:
  infantry, cavalry, ranged, siege, spademen, fascine-carriers, engineers, pontoon-train, crossing-marshals.
"""
import argparse
import json
import math
import os
import struct
import sys
import zlib

import bpy  # first: under the PyPI bpy module, bmesh only becomes importable once bpy is loaded
import bmesh  # noqa: E402
from mathutils import Euler, Matrix, Vector  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_cli  # noqa: E402

FPS = 30
CLIP_FRAMES = 30

# Rigs: (name, head, tail, parent) in Z-up metres, facing -Y, left = +X, right = -X.
HUMANOID_BONES = [
    ("root", (0.0, 0.0, 0.0), (0.0, 0.3, 0.0), None),
    ("hips", (0.0, 0.0, 0.88), (0.0, 0.0, 1.05), "root"),
    ("spine", (0.0, 0.0, 1.05), (0.0, 0.0, 1.25), "hips"),
    ("chest", (0.0, 0.0, 1.25), (0.0, 0.0, 1.48), "spine"),
    ("head", (0.0, 0.0, 1.50), (0.0, 0.0, 1.82), "chest"),
    ("upper_arm.L", (0.24, 0.0, 1.44), (0.28, 0.0, 1.15), "chest"),
    ("forearm.L", (0.28, 0.0, 1.15), (0.28, -0.15, 0.94), "upper_arm.L"),
    ("upper_arm.R", (-0.24, 0.0, 1.44), (-0.28, 0.0, 1.15), "chest"),
    ("forearm.R", (-0.28, 0.0, 1.15), (-0.28, -0.15, 0.94), "upper_arm.R"),
    ("thigh.L", (0.11, 0.0, 0.88), (0.11, 0.0, 0.48), "hips"),
    ("shin.L", (0.11, 0.0, 0.48), (0.11, 0.0, 0.04), "thigh.L"),
    ("thigh.R", (-0.11, 0.0, 0.88), (-0.11, 0.0, 0.48), "hips"),
    ("shin.R", (-0.11, 0.0, 0.48), (-0.11, 0.0, 0.04), "thigh.R"),
]

QUADRUPED_BONES = [
    ("root", (0.0, 0.0, 0.0), (0.0, 0.4, 0.0), None),
    ("pelvis", (0.0, 0.45, 1.05), (0.0, 0.15, 1.08), "root"),
    ("spine", (0.0, 0.15, 1.08), (0.0, -0.25, 1.10), "pelvis"),
    ("chest", (0.0, -0.25, 1.10), (0.0, -0.58, 1.15), "spine"),
    ("neck", (0.0, -0.58, 1.18), (0.0, -0.82, 1.56), "chest"),
    ("head", (0.0, -0.82, 1.56), (0.0, -1.12, 1.44), "neck"),
    ("tail", (0.0, 0.62, 1.12), (0.0, 0.92, 0.65), "pelvis"),
    ("front_thigh.L", (0.18, -0.45, 1.02), (0.18, -0.45, 0.52), "chest"),
    ("front_shin.L", (0.18, -0.45, 0.52), (0.18, -0.45, 0.04), "front_thigh.L"),
    ("front_thigh.R", (-0.18, -0.45, 1.02), (-0.18, -0.45, 0.52), "chest"),
    ("front_shin.R", (-0.18, -0.45, 0.52), (-0.18, -0.45, 0.04), "front_thigh.R"),
    ("hind_thigh.L", (0.18, 0.48, 1.02), (0.18, 0.50, 0.52), "pelvis"),
    ("hind_shin.L", (0.18, 0.50, 0.52), (0.18, 0.50, 0.04), "hind_thigh.L"),
    ("hind_thigh.R", (-0.18, 0.48, 1.02), (-0.18, 0.50, 0.52), "pelvis"),
    ("hind_shin.R", (-0.18, 0.50, 0.52), (-0.18, 0.50, 0.04), "hind_thigh.R"),
    ("rider_torso", (0.0, 0.0, 1.32), (0.0, 0.0, 1.88), "spine"),
    ("rider_head", (0.0, 0.0, 1.88), (0.0, 0.0, 2.22), "rider_torso"),
    ("rider_arm.R", (-0.26, 0.0, 1.78), (-0.30, -0.25, 1.55), "rider_torso"),
]

SIEGE_BONES = [
    ("root", (0.0, 0.0, 0.0), (0.0, 0.4, 0.0), None),
    ("frame", (0.0, 0.0, 0.55), (0.0, 0.0, 1.20), "root"),
    ("arm", (0.0, -0.10, 1.05), (0.0, 0.85, 2.05), "frame"),
    ("wheel_fl", (0.95, -0.85, 0.48), (1.15, -0.85, 0.48), "frame"),
    ("wheel_fr", (-0.95, -0.85, 0.48), (-1.15, -0.85, 0.48), "frame"),
    ("wheel_bl", (0.95, 0.85, 0.48), (1.15, 0.85, 0.48), "frame"),
    ("wheel_br", (-0.95, 0.85, 0.48), (-1.15, 0.85, 0.48), "frame"),
]


def hex_to_rgb_bytes(hex_str):
    s = hex_str.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16))


def write_palette_png(palette):
    """Write a 256x256 RGBA PNG for the 16x16 palette grid using Python stdlib only."""
    grid = palette["grid"]
    cell_px = 16
    size = grid * cell_px
    grid_colors = [[(128, 128, 128) for _ in range(grid)] for _ in range(grid)]
    colors = palette.get("colors", {})
    for name, (cx, cy) in palette["cells"].items():
        if name in colors:
            grid_colors[cy][cx] = hex_to_rgb_bytes(colors[name])

    raw_rows = bytearray()
    for py in range(size):
        raw_rows.append(0)  # PNG filter type 0 (None)
        # PNG y=0 is top (UV v=1, cy=grid-1); y=size-1 is bottom (UV v=0, cy=0).
        cy = grid - 1 - (py // cell_px)
        for px in range(size):
            cx = px // cell_px
            r, g, b = grid_colors[cy][cx]
            raw_rows.extend((r, g, b, 255))

    def chunk(tag, data):
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    idat = zlib.compress(bytes(raw_rows), 9)
    png_bytes = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")

    out_path = os.path.join(blender_cli.REPO_ROOT, palette["texture"])
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(png_bytes)
    return out_path


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def make_rig(scene, rig_name, bone_defs):
    armature = bpy.data.armatures.new(rig_name)
    rig = bpy.data.objects.new(rig_name, armature)
    scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in bone_defs:
        bone = armature.edit_bones.new(name)
        bone.head, bone.tail = head, tail
        if parent:
            bone.parent = armature.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def make_palette_material(palette_png_path):
    material = bpy.data.materials.new("M_palette")
    if material.node_tree is None:
        material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    out = nodes.new("ShaderNodeOutputMaterial")
    out.location = (500, 0)

    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (220, 0)
    bsdf.inputs["Roughness"].default_value = 0.72
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25

    uv = nodes.new("ShaderNodeTexCoord")
    uv.location = (-520, 120)

    tex = nodes.new("ShaderNodeTexImage")
    tex.location = (-280, 120)
    tex.interpolation = "Closest"
    if os.path.exists(palette_png_path):
        tex.image = bpy.data.images.load(palette_png_path, check_existing=True)

    mask_attr = nodes.new("ShaderNodeAttribute")
    mask_attr.location = (-280, -140)
    mask_attr.attribute_name = "TeamMask"

    team_col = nodes.new("ShaderNodeRGB")
    team_col.location = (-280, -320)
    team_col.outputs[0].default_value = (0.85, 0.28, 0.16, 1.0)

    mix = nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.location = (-20, 40)

    links.new(uv.outputs["UV"], tex.inputs["Vector"])
    links.new(tex.outputs["Color"], mix.inputs["A"])
    links.new(team_col.outputs["Color"], mix.inputs["B"])
    links.new(mask_attr.outputs["Fac"], mix.inputs["Factor"])
    links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return material


def _record_action(rig, scene, name, frame_step, build_frame_fn):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = action

    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)

    frames = list(range(1, CLIP_FRAMES + 2, frame_step))
    if frames[-1] != CLIP_FRAMES + 1:
        frames.append(CLIP_FRAMES + 1)

    for frame in frames:
        t = (frame - 1) / CLIP_FRAMES
        rot_keys, loc_keys = build_frame_fn(t)
        for bone_name, rot in rot_keys.items():
            pb = rig.pose.bones[bone_name]
            pb.rotation_euler = rot
            pb.keyframe_insert(data_path="rotation_euler", frame=frame)
        for bone_name, loc in loc_keys.items():
            pb = rig.pose.bones[bone_name]
            pb.location = loc
            pb.keyframe_insert(data_path="location", frame=frame)

    bpy.ops.object.mode_set(mode="OBJECT")
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_euler = (0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
    scene.frame_start, scene.frame_end = 1, CLIP_FRAMES + 1
    return action


def _pulse(t, peak, sharp):
    return math.exp(-((t - peak) * sharp) ** 2)


def make_humanoid_anims(rig, scene):
    def walk_frame(t):
        phase = 2 * math.pi * t
        thigh_swing = math.radians(35) * math.sin(phase)
        arm_swing = math.radians(28) * math.sin(phase)
        rot = {
            "thigh.L": (thigh_swing, 0.0, 0.0),
            "thigh.R": (-thigh_swing, 0.0, 0.0),
            "shin.L": (math.radians(3) + max(0.0, -math.sin(phase)) * math.radians(44), 0.0, 0.0),
            "shin.R": (math.radians(3) + max(0.0, math.sin(phase)) * math.radians(44), 0.0, 0.0),
            "upper_arm.L": (-arm_swing, 0.0, 0.0),
            "upper_arm.R": (arm_swing, 0.0, 0.0),
            "forearm.L": (-math.radians(12) - math.radians(25) * max(0.0, math.sin(phase)), 0.0, 0.0),
            "forearm.R": (-math.radians(12) - math.radians(25) * max(0.0, -math.sin(phase)), 0.0, 0.0),
            "chest": (math.radians(3.5), math.radians(8) * math.sin(phase), 0.0),
            "head": (-math.radians(2.0), -math.radians(5) * math.sin(phase), 0.0),
        }
        loc = {"hips": (0.0, 0.055 * abs(math.sin(phase)), 0.0)}
        return rot, loc

    def strike_frame(t):
        # Wind-up coil -> explosive overhead slam & deep forward lunge -> recoil recovery.
        coil = _pulse(t, 0.22, 6.5)
        smash = _pulse(t, 0.46, 8.0)
        lunge = smash * 0.24 - coil * 0.12
        drop = -smash * 0.14 + coil * 0.06
        # upper_arm local X: -90 deg is straight forward, -160 deg is high overhead, -20 deg is low front strike.
        arm_r_pitch = math.radians(-158) * coil + math.radians(-18) * smash + math.radians(-52) * (1.0 - max(coil, smash))
        forearm_r = math.radians(-55) * coil + math.radians(-10) * smash - math.radians(24)
        rot = {
            "chest": (math.radians(-24) * coil + math.radians(36) * smash, math.radians(-22) * coil + math.radians(28) * smash, 0.0),
            "head": (math.radians(14) * coil - math.radians(18) * smash, 0.0, 0.0),
            "upper_arm.R": (arm_r_pitch, math.radians(22) * smash, math.radians(-16) * coil),
            "forearm.R": (forearm_r, 0.0, 0.0),
            "upper_arm.L": (math.radians(-45) * coil - math.radians(95) * smash, 0.0, math.radians(18)),
            "forearm.L": (math.radians(-38) - math.radians(30) * smash, 0.0, 0.0),
            "thigh.L": (math.radians(-42) * smash + math.radians(22) * coil, 0.0, 0.0),
            "shin.L": (math.radians(46) * smash + math.radians(12), 0.0, 0.0),
            "thigh.R": (math.radians(38) * smash - math.radians(16) * coil, 0.0, 0.0),
            "shin.R": (math.radians(22) * coil + math.radians(14), 0.0, 0.0),
        }
        loc = {"hips": (0.0, drop, lunge)}
        return rot, loc

    def bash_frame(t):
        # Explosive shield bash / snapshot + wide sweeping side cleave with lateral sidestep hop.
        phase = 2 * math.pi * t
        bash = _pulse(t, 0.30, 7.5)
        cleave = _pulse(t, 0.68, 7.5)
        rot = {
            "chest": (math.radians(20) * bash + math.radians(24) * cleave, math.radians(-42) * bash + math.radians(44) * cleave, math.radians(12) * math.sin(phase)),
            "head": (0.0, math.radians(22) * bash - math.radians(26) * cleave, 0.0),
            "upper_arm.L": (math.radians(-115) * bash + math.radians(-35) * cleave, math.radians(-25) * bash, math.radians(32) * bash),
            "forearm.L": (math.radians(-50) + math.radians(-35) * bash, 0.0, 0.0),
            "upper_arm.R": (math.radians(-135) * bash + math.radians(-28) * cleave, 0.0, math.radians(-46) * cleave),
            "forearm.R": (math.radians(-48) * bash - math.radians(15) * cleave, 0.0, 0.0),
            "thigh.L": (math.radians(-44) * bash + math.radians(26) * cleave, 0.0, math.radians(16) * math.sin(phase)),
            "shin.L": (math.radians(40) * bash + math.radians(16), 0.0, 0.0),
            "thigh.R": (math.radians(34) * bash - math.radians(34) * cleave, 0.0, math.radians(-16) * math.sin(phase)),
            "shin.R": (math.radians(42) * cleave + math.radians(16), 0.0, 0.0),
        }
        loc = {"hips": (0.14 * math.sin(phase), 0.11 * max(bash, cleave) - 0.08 * abs(math.cos(phase)), (bash + cleave) * 0.18)}
        return rot, loc

    def flurry_frame(t):
        # Twin rapid forward thrusts + aggressive high front knee kick / brace cycle.
        p1 = _pulse(t, 0.24, 9.0)
        p2 = _pulse(t, 0.56, 9.0)
        kick = _pulse(t, 0.80, 7.5)
        thrust = max(p1, p2)
        retract = _pulse(t, 0.40, 10.0)
        rot = {
            "chest": (math.radians(28) * thrust - math.radians(18) * kick, math.radians(32) * p1 - math.radians(26) * p2, 0.0),
            "head": (math.radians(-12) * thrust, 0.0, 0.0),
            "upper_arm.R": (math.radians(-105) * thrust + math.radians(-15) * retract, 0.0, math.radians(-18) * p2),
            "forearm.R": (math.radians(-10) * thrust + math.radians(-78) * retract, 0.0, 0.0),
            "upper_arm.L": (math.radians(-48) + math.radians(-52) * kick, 0.0, math.radians(24) * p1),
            "forearm.L": (math.radians(-45), 0.0, 0.0),
            "thigh.L": (math.radians(-68) * kick - math.radians(28) * p1, 0.0, 0.0),
            "shin.L": (math.radians(60) * kick + math.radians(14), 0.0, 0.0),
            "thigh.R": (math.radians(36) * p2 + math.radians(16) * kick, 0.0, 0.0),
            "shin.R": (math.radians(34) * p1 + math.radians(12), 0.0, 0.0),
        }
        loc = {"hips": (0.08 * (p1 - p2), -0.10 * thrust + 0.09 * kick, 0.22 * thrust - 0.12 * retract)}
        return rot, loc

    def death_frame(t):
        # Struck backward sprawl: chest/head snap back, arms fling wide, hips crash onto turf.
        fall = min(1.0, max(0.0, (t - 0.14) / 0.58))
        s = fall * fall * (3.0 - 2.0 * fall)
        hit = _pulse(t, 0.14, 9.0)
        bounce = _pulse(t, 0.76, 12.0)
        rot = {
            "hips": (math.radians(-82) * s, math.radians(14) * s, 0.0),
            "chest": (math.radians(-28) * hit + math.radians(8) * s, math.radians(-16) * hit, 0.0),
            "head": (math.radians(-32) * hit - math.radians(10) * s, 0.0, 0.0),
            "upper_arm.L": (
                math.radians(-75) * hit + math.radians(-35) * s,
                0.0,
                math.radians(55) * hit + math.radians(52) * s,
            ),
            "forearm.L": (math.radians(-20) * s, 0.0, 0.0),
            "upper_arm.R": (
                math.radians(-75) * hit + math.radians(-28) * s,
                0.0,
                math.radians(-55) * hit + math.radians(-48) * s,
            ),
            "forearm.R": (math.radians(-20) * s, 0.0, 0.0),
            "thigh.L": (math.radians(12) * s, 0.0, math.radians(14) * s),
            "shin.L": (math.radians(24) * s, 0.0, 0.0),
            "thigh.R": (math.radians(18) * s, 0.0, math.radians(-14) * s),
            "shin.R": (math.radians(24) * s, 0.0, 0.0),
        }
        loc = {"hips": (0.08 * s, -0.71 * s + 0.05 * bounce, -0.32 * s)}
        return rot, loc

    def collapse_frame(t):
        # Spinning knee-buckle into a face-down crumple.
        kneel = _pulse(t, 0.26, 6.5)
        facefall = min(1.0, max(0.0, (t - 0.24) / 0.52))
        f = facefall * facefall * (3.0 - 2.0 * facefall)
        rot = {
            "hips": (
                math.radians(83) * f,
                math.radians(-34) * kneel + math.radians(-22) * f,
                math.radians(14) * f,
            ),
            "chest": (math.radians(24) * kneel + math.radians(-8) * f, math.radians(18) * kneel, 0.0),
            "head": (math.radians(20) * kneel - math.radians(18) * f, 0.0, 0.0),
            "upper_arm.L": (math.radians(-42) * kneel + math.radians(-125) * f, 0.0, math.radians(38) * f),
            "forearm.L": (math.radians(-48) * kneel - math.radians(18) * f, 0.0, 0.0),
            "upper_arm.R": (math.radians(-38) * kneel + math.radians(-115) * f, 0.0, math.radians(-42) * f),
            "forearm.R": (math.radians(-45) * kneel - math.radians(22) * f, 0.0, 0.0),
            "thigh.L": (math.radians(-58) * kneel + math.radians(-12) * f, 0.0, math.radians(10) * f),
            "shin.L": (math.radians(88) * kneel + math.radians(16) * f, 0.0, 0.0),
            "thigh.R": (math.radians(-52) * kneel + math.radians(-8) * f, 0.0, math.radians(-10) * f),
            "shin.R": (math.radians(84) * kneel + math.radians(14) * f, 0.0, 0.0),
        }
        loc = {"hips": (-0.10 * f, -0.36 * kneel * (1.0 - f) - 0.72 * f, 0.28 * f)}
        return rot, loc

    walk_action = _record_action(rig, scene, "A_humanoid_walk", 3, walk_frame)
    _record_action(rig, scene, "A_humanoid_strike", 2, strike_frame)
    _record_action(rig, scene, "A_humanoid_bash", 2, bash_frame)
    _record_action(rig, scene, "A_humanoid_flurry", 2, flurry_frame)
    _record_action(rig, scene, "A_humanoid_death", 2, death_frame)
    _record_action(rig, scene, "A_humanoid_collapse", 2, collapse_frame)
    rig.animation_data.action = walk_action
    return walk_action


def make_quadruped_anims(rig, scene):
    def trot_frame(t):
        phase = 2 * math.pi * t
        s = math.radians(35) * math.sin(phase)
        rot = {
            "front_thigh.L": (s, 0.0, 0.0),
            "front_shin.L": (max(0.0, -math.sin(phase)) * math.radians(45), 0.0, 0.0),
            "hind_thigh.R": (s, 0.0, 0.0),
            "hind_shin.R": (max(0.0, -math.sin(phase)) * math.radians(42), 0.0, 0.0),
            "front_thigh.R": (-s, 0.0, 0.0),
            "front_shin.R": (max(0.0, math.sin(phase)) * math.radians(45), 0.0, 0.0),
            "hind_thigh.L": (-s, 0.0, 0.0),
            "hind_shin.L": (max(0.0, math.sin(phase)) * math.radians(42), 0.0, 0.0),
            "neck": (math.radians(-6) + math.radians(8) * math.sin(phase * 2), 0.0, 0.0),
            "rider_torso": (math.radians(3) + math.radians(7) * math.cos(phase * 2), 0.0, 0.0),
        }
        # pelvis bone local: X=side, +Y=forward, -Z=up.
        loc = {"pelvis": (0.0, 0.0, -0.065 * abs(math.sin(phase * 2)))}
        return rot, loc

    def rear_frame(t):
        # Warhorse rears high and rider slams lance forward-down!
        rear = _pulse(t, 0.36, 5.5)
        slam = _pulse(t, 0.58, 7.5)
        rot = {
            "pelvis": (math.radians(-26) * rear + math.radians(10) * slam, 0.0, 0.0),
            "chest": (math.radians(-14) * rear + math.radians(12) * slam, 0.0, 0.0),
            "neck": (math.radians(-18) * rear + math.radians(24) * slam, 0.0, 0.0),
            "head": (math.radians(14) * rear - math.radians(12) * slam, 0.0, 0.0),
            "front_thigh.L": (math.radians(-58) * rear + math.radians(25) * slam, 0.0, 0.0),
            "front_shin.L": (math.radians(68) * rear, 0.0, 0.0),
            "front_thigh.R": (math.radians(-44) * _pulse(t, 0.44, 6.0) + math.radians(28) * slam, 0.0, 0.0),
            "front_shin.R": (math.radians(62) * _pulse(t, 0.44, 6.0), 0.0, 0.0),
            "hind_thigh.L": (math.radians(28) * rear - math.radians(16) * slam, 0.0, 0.0),
            "hind_thigh.R": (math.radians(24) * rear - math.radians(16) * slam, 0.0, 0.0),
            "rider_torso": (math.radians(-18) * rear + math.radians(36) * slam, math.radians(20) * slam, 0.0),
            "rider_arm.R": (math.radians(-88) * rear + math.radians(35) * slam, 0.0, 0.0),
        }
        loc = {"pelvis": (0.0, 0.18 * slam, -0.24 * rear + 0.06 * slam)}
        return rot, loc

    def slash_frame(t):
        # Horse checks and lunges sideways while rider sweeps lance/sword wide across saddle.
        phase = 2 * math.pi * t
        sweep = _pulse(t, 0.48, 6.5)
        rot = {
            "pelvis": (math.radians(8) * math.sin(phase), math.radians(14) * math.sin(phase), math.radians(14) * math.cos(phase)),
            "neck": (math.radians(12) * math.sin(phase), math.radians(-18) * math.cos(phase), 0.0),
            "front_thigh.L": (math.radians(-38) * math.sin(phase), 0.0, math.radians(16) * sweep),
            "front_shin.L": (max(0.0, -math.sin(phase)) * math.radians(52), 0.0, 0.0),
            "front_thigh.R": (math.radians(38) * math.sin(phase), 0.0, math.radians(-16) * sweep),
            "front_shin.R": (max(0.0, math.sin(phase)) * math.radians(52), 0.0, 0.0),
            "hind_thigh.L": (math.radians(34) * math.sin(phase), 0.0, 0.0),
            "hind_thigh.R": (math.radians(-34) * math.sin(phase), 0.0, 0.0),
            "rider_torso": (math.radians(24) * sweep, math.radians(40) * math.sin(phase), math.radians(12) * math.cos(phase)),
            "rider_arm.R": (math.radians(-58) + math.radians(-42) * sweep, 0.0, math.radians(52) * math.sin(phase)),
        }
        loc = {"pelvis": (0.14 * math.sin(phase), 0.22 * sweep, -0.09 * abs(math.cos(phase)))}
        return rot, loc

    def charge_frame(t):
        # Explosive bounding gallop leap and low couched lance thrust.
        phase = 2 * math.pi * t
        leap = max(0.0, math.sin(phase))
        push = max(0.0, -math.sin(phase))
        thrust = _pulse(t, 0.42, 6.0)
        rot = {
            "pelvis": (math.radians(-14) * leap + math.radians(14) * push, 0.0, 0.0),
            "neck": (math.radians(18) * leap, 0.0, 0.0),
            "front_thigh.L": (math.radians(-58) * leap + math.radians(34) * push, 0.0, 0.0),
            "front_shin.L": (math.radians(45) * push, 0.0, 0.0),
            "front_thigh.R": (math.radians(-48) * leap + math.radians(40) * push, 0.0, 0.0),
            "front_shin.R": (math.radians(48) * push, 0.0, 0.0),
            "hind_thigh.L": (math.radians(48) * leap - math.radians(34) * push, 0.0, 0.0),
            "hind_thigh.R": (math.radians(40) * leap - math.radians(40) * push, 0.0, 0.0),
            "rider_torso": (math.radians(34) * thrust + math.radians(14), 0.0, 0.0),
            "rider_arm.R": (math.radians(-38) + math.radians(-52) * thrust, 0.0, 0.0),
        }
        loc = {"pelvis": (0.0, 0.28 * thrust, -0.22 * leap + 0.06 * push)}
        return rot, loc

    def death_frame(t):
        # Rear up slightly, then crash and roll onto left flank with rider slumped sideways.
        rear = _pulse(t, 0.18, 7.0)
        fall = min(1.0, max(0.0, (t - 0.20) / 0.56))
        s = fall * fall * (3.0 - 2.0 * fall)
        rot = {
            "pelvis": (
                math.radians(-26) * rear + math.radians(-8) * s,
                math.radians(82) * s,
                math.radians(12) * s,
            ),
            "chest": (math.radians(-12) * rear + math.radians(14) * s, math.radians(10) * s, 0.0),
            "neck": (math.radians(-20) * rear + math.radians(24) * s, math.radians(18) * s, 0.0),
            "head": (math.radians(16) * rear + math.radians(-18) * s, 0.0, 0.0),
            "front_thigh.L": (math.radians(-48) * rear + math.radians(28) * s, 0.0, math.radians(24) * s),
            "front_shin.L": (math.radians(52) * rear + math.radians(34) * s, 0.0, 0.0),
            "front_thigh.R": (math.radians(-40) * rear - math.radians(26) * s, 0.0, math.radians(-28) * s),
            "front_shin.R": (math.radians(48) * rear + math.radians(22) * s, 0.0, 0.0),
            "hind_thigh.L": (math.radians(22) * rear + math.radians(26) * s, 0.0, math.radians(22) * s),
            "hind_shin.L": (math.radians(26) * s, 0.0, 0.0),
            "hind_thigh.R": (math.radians(18) * rear - math.radians(20) * s, 0.0, math.radians(-24) * s),
            "hind_shin.R": (math.radians(32) * s, 0.0, 0.0),
            "rider_torso": (math.radians(-14) * rear + math.radians(38) * s, math.radians(28) * s, math.radians(42) * s),
            "rider_head": (math.radians(-12) * s, 0.0, 0.0),
            "rider_arm.R": (math.radians(-75) * s, 0.0, math.radians(-40) * s),
        }
        loc = {"pelvis": (0.22 * s, 0.14 * s, -0.20 * rear * (1.0 - s) + 0.78 * s)}
        return rot, loc

    def collapse_frame(t):
        # Front-leg buckle and right-side rollover with rider pitched forward-right.
        buckle = _pulse(t, 0.24, 6.5)
        fall = min(1.0, max(0.0, (t - 0.22) / 0.54))
        s = fall * fall * (3.0 - 2.0 * fall)
        rot = {
            "pelvis": (
                math.radians(32) * buckle + math.radians(10) * s,
                math.radians(-82) * s,
                math.radians(-14) * s,
            ),
            "chest": (math.radians(18) * buckle + math.radians(16) * s, math.radians(-12) * s, 0.0),
            "neck": (math.radians(24) * buckle + math.radians(18) * s, math.radians(-22) * s, 0.0),
            "head": (math.radians(-18) * s, 0.0, 0.0),
            "front_thigh.L": (math.radians(45) * buckle - math.radians(32) * s, 0.0, math.radians(26) * s),
            "front_shin.L": (math.radians(60) * buckle + math.radians(26) * s, 0.0, 0.0),
            "front_thigh.R": (math.radians(42) * buckle + math.radians(24) * s, 0.0, math.radians(-24) * s),
            "front_shin.R": (math.radians(56) * buckle + math.radians(32) * s, 0.0, 0.0),
            "hind_thigh.L": (math.radians(-26) * buckle - math.radians(22) * s, 0.0, math.radians(24) * s),
            "hind_shin.L": (math.radians(28) * s, 0.0, 0.0),
            "hind_thigh.R": (math.radians(-22) * buckle + math.radians(28) * s, 0.0, math.radians(-24) * s),
            "hind_shin.R": (math.radians(24) * s, 0.0, 0.0),
            "rider_torso": (math.radians(22) * buckle + math.radians(52) * s, math.radians(-24) * s, math.radians(-38) * s),
            "rider_head": (math.radians(18) * s, 0.0, 0.0),
            "rider_arm.R": (math.radians(-68) * s, 0.0, math.radians(38) * s),
        }
        loc = {"pelvis": (-0.22 * s, 0.28 * s, 0.40 * buckle * (1.0 - s) + 0.80 * s)}
        return rot, loc

    trot_action = _record_action(rig, scene, "A_quadruped_trot", 3, trot_frame)
    _record_action(rig, scene, "A_quadruped_rear", 2, rear_frame)
    _record_action(rig, scene, "A_quadruped_slash", 2, slash_frame)
    _record_action(rig, scene, "A_quadruped_charge", 2, charge_frame)
    _record_action(rig, scene, "A_quadruped_death", 2, death_frame)
    _record_action(rig, scene, "A_quadruped_collapse", 2, collapse_frame)
    rig.animation_data.action = trot_action
    return trot_action


def make_siege_anims(rig, scene):
    def roll_frame(t):
        wheel_spin = 2 * math.pi * t
        rot = {w: (0.0, wheel_spin, 0.0) for w in ("wheel_fl", "wheel_fr", "wheel_bl", "wheel_br")}
        rot["frame"] = (math.radians(2.5) * math.sin(4 * math.pi * t), 0.0, 0.0)
        rot["arm"] = (math.radians(10) * math.sin(4 * math.pi * t), 0.0, 0.0)
        loc = {"frame": (0.0, 0.035 * abs(math.sin(4 * math.pi * t)), 0.0)}
        return rot, loc

    def fire_frame(t):
        # Winch arm back slowly, then snap violently forward making the chassis buck off the ground!
        winch = _pulse(t, 0.28, 4.5)
        snap = _pulse(t, 0.50, 9.5)
        recoil = _pulse(t, 0.62, 7.0)
        wheel_jolt = math.radians(-28) * snap + math.radians(22) * recoil
        rot = {w: (0.0, wheel_jolt, 0.0) for w in ("wheel_fl", "wheel_fr", "wheel_bl", "wheel_br")}
        rot["arm"] = (math.radians(-38) * winch + math.radians(78) * snap, 0.0, 0.0)
        rot["frame"] = (math.radians(-14) * snap + math.radians(8) * recoil, 0.0, 0.0)
        loc = {"frame": (0.0, 0.18 * snap + 0.06 * recoil, -0.14 * recoil)}
        return rot, loc

    def heave_frame(t):
        # Heavy double-heave rocking & angled shot.
        phase = 2 * math.pi * t
        snap = _pulse(t, 0.42, 8.0)
        rot = {w: (0.0, math.radians(25) * math.sin(phase), 0.0) for w in ("wheel_fl", "wheel_fr", "wheel_bl", "wheel_br")}
        rot["arm"] = (math.radians(-30) * max(0.0, -math.sin(phase)) + math.radians(64) * snap, 0.0, math.radians(8) * snap)
        rot["frame"] = (math.radians(11) * math.sin(phase * 2), math.radians(5) * math.sin(phase), 0.0)
        loc = {"frame": (0.0, 0.12 * abs(math.sin(phase * 2)), 0.08 * math.sin(phase))}
        return rot, loc

    def salvo_frame(t):
        # Early high-energy snap + deep wheeled recoil slide & return (frame local: +Y=up, -Z=back).
        snap = _pulse(t, 0.30, 10.0)
        kick = _pulse(t, 0.46, 5.5)
        rot = {w: (0.0, math.radians(-55) * kick, 0.0) for w in ("wheel_fl", "wheel_fr", "wheel_bl", "wheel_br")}
        rot["arm"] = (math.radians(84) * snap - math.radians(24) * (1.0 - snap), 0.0, 0.0)
        rot["frame"] = (math.radians(-16) * kick + math.radians(6) * snap, 0.0, 0.0)
        loc = {"frame": (0.0, 0.15 * snap, -0.24 * kick)}
        return rot, loc

    def death_frame(t):
        # Blast jolt upward, then chassis cants onto broken side with wheels splayed and arm snapped.
        jolt = _pulse(t, 0.14, 9.0)
        wreck = min(1.0, max(0.0, (t - 0.16) / 0.56))
        s = wreck * wreck * (3.0 - 2.0 * wreck)
        rot = {
            "frame": (math.radians(24) * s, math.radians(16) * s, math.radians(38) * s),
            "arm": (math.radians(92) * s, 0.0, math.radians(26) * s),
            "wheel_fl": (0.0, math.radians(-45) * s, math.radians(52) * s),
            "wheel_br": (0.0, math.radians(-45) * s, math.radians(52) * s),
            "wheel_fr": (0.0, math.radians(45) * s, math.radians(-52) * s),
            "wheel_bl": (0.0, math.radians(45) * s, math.radians(-52) * s),
        }
        loc = {"frame": (0.14 * s, 0.18 * jolt * (1.0 - s) - 0.36 * s, 0.16 * s)}
        return rot, loc

    def collapse_frame(t):
        # Torsion snap and rollover wreck onto the opposite flank.
        jolt = _pulse(t, 0.16, 8.5)
        wreck = min(1.0, max(0.0, (t - 0.18) / 0.56))
        s = wreck * wreck * (3.0 - 2.0 * wreck)
        rot = {
            "frame": (math.radians(-20) * s, math.radians(-22) * s, math.radians(-48) * s),
            "arm": (math.radians(78) * s, math.radians(32) * s, math.radians(-30) * s),
            "wheel_fl": (0.0, math.radians(60) * s, math.radians(-48) * s),
            "wheel_br": (0.0, math.radians(60) * s, math.radians(-48) * s),
            "wheel_fr": (0.0, math.radians(-60) * s, math.radians(48) * s),
            "wheel_bl": (0.0, math.radians(-60) * s, math.radians(48) * s),
        }
        loc = {"frame": (-0.18 * s, 0.14 * jolt * (1.0 - s) - 0.38 * s, -0.18 * s)}
        return rot, loc

    roll_action = _record_action(rig, scene, "A_siege_roll", 3, roll_frame)
    _record_action(rig, scene, "A_siege_fire", 2, fire_frame)
    _record_action(rig, scene, "A_siege_heave", 2, heave_frame)
    _record_action(rig, scene, "A_siege_salvo", 2, salvo_frame)
    _record_action(rig, scene, "A_siege_death", 2, death_frame)
    _record_action(rig, scene, "A_siege_collapse", 2, collapse_frame)
    rig.animation_data.action = roll_action
    return roll_action


def _basis_for_segment(p0, p1):
    v0 = Vector(p0)
    v1 = Vector(p1)
    delta = v1 - v0
    length = delta.length
    if length < 1e-6:
        return v0, Matrix.Identity(4), 0.001
    z_axis = delta.normalized()
    up_ref = Vector((0.0, 0.0, 1.0)) if abs(z_axis.z) < 0.95 else Vector((1.0, 0.0, 0.0))
    x_axis = up_ref.cross(z_axis).normalized()
    y_axis = z_axis.cross(x_axis).normalized()
    mat = Matrix((
        (x_axis.x, y_axis.x, z_axis.x, (v0.x + v1.x) * 0.5),
        (x_axis.y, y_axis.y, z_axis.y, (v0.y + v1.y) * 0.5),
        (x_axis.z, y_axis.z, z_axis.z, (v0.z + v1.z) * 0.5),
        (0.0, 0.0, 0.0, 1.0),
    ))
    return v0, mat, length


class UnitBuilder:
    """Procedural low-poly builder tracking per-face palette cell, TeamMask and per-vertex bone group."""

    def __init__(self, palette, bone_names):
        self.bm = bmesh.new()
        self.palette = palette
        self.cells = palette["cells"]
        self.grid = palette["grid"]
        self.bone_index = {name: i for i, name in enumerate(bone_names)}
        self.bone_names = list(bone_names)
        self.deform = self.bm.verts.layers.deform.verify()
        self.cell_layer = self.bm.faces.layers.int.new("cell_idx")
        self.team_layer = self.bm.faces.layers.int.new("team_flag")
        self.vert_bone = {}  # BMVert -> bone index

    def _cell_code(self, cell_name):
        cx, cy = self.cells[cell_name]
        return cx + self.grid * cy

    def _tag_geom(self, geom_verts, geom_faces, cell_name, team, bone_name):
        code = self._cell_code(cell_name)
        t = 1 if team else 0
        b_idx = self.bone_index[bone_name]
        for v in geom_verts:
            v[self.deform][b_idx] = 1.0
            self.vert_bone[v] = b_idx
        for f in geom_faces:
            f[self.cell_layer] = code
            f[self.team_layer] = t

    def add_box(self, center, size, rot_deg, cell, team, bone):
        mat = (
            Matrix.Translation(Vector(center))
            @ Euler(tuple(math.radians(a) for a in rot_deg), "XYZ").to_matrix().to_4x4()
            @ Matrix.Diagonal((size[0], size[1], size[2], 1.0))
        )
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=mat)
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_geom(verts, faces, cell, team, bone)

    def add_tapered_box(self, center, bottom_xy, top_xy, height, rot_deg, cell, team, bone):
        bx, by = bottom_xy[0] * 0.5, bottom_xy[1] * 0.5
        tx, ty = top_xy[0] * 0.5, top_xy[1] * 0.5
        hz = height * 0.5
        coords = [
            (-bx, -by, -hz), (bx, -by, -hz), (bx, by, -hz), (-bx, by, -hz),
            (-tx, -ty, hz), (tx, -ty, hz), (tx, ty, hz), (-tx, ty, hz),
        ]
        mat = (
            Matrix.Translation(Vector(center))
            @ Euler(tuple(math.radians(a) for a in rot_deg), "XYZ").to_matrix().to_4x4()
        )
        verts = [self.bm.verts.new(mat @ Vector(c)) for c in coords]
        quad_indices = [
            (0, 3, 2, 1),
            (4, 5, 6, 7),
            (0, 1, 5, 4),
            (1, 2, 6, 5),
            (2, 3, 7, 6),
            (3, 0, 4, 7),
        ]
        faces = [self.bm.faces.new([verts[i] for i in q]) for q in quad_indices]
        self._tag_geom(verts, faces, cell, team, bone)

    def add_cylinder(self, p0, p1, r0, r1, segments, cell, team, bone, cap_ends=True):
        _, mat, length = _basis_for_segment(p0, p1)
        res = bmesh.ops.create_cone(
            self.bm,
            cap_ends=cap_ends,
            cap_tris=False,
            segments=max(3, segments),
            radius1=r0,
            radius2=r1,
            depth=length,
            matrix=mat,
        )
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_geom(verts, faces, cell, team, bone)

    def add_sphere(self, center, radii_xyz, segments, rings, cell, team, bone):
        mat = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((radii_xyz[0], radii_xyz[1], radii_xyz[2], 1.0))
        res = bmesh.ops.create_uvsphere(
            self.bm,
            u_segments=max(4, segments),
            v_segments=max(3, rings),
            radius=1.0,
            matrix=mat,
        )
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_geom(verts, faces, cell, team, bone)

    def add_wheel(self, center, width, radius, spokes, tire_cell, wood_cell, team, bone, simple=False):
        cx, cy, cz = center
        half_w = width * 0.5
        if simple:
            # Solid tire + raised wooden disc + hub for LOD1 budget (~76 tris/wheel).
            self.add_cylinder((cx - half_w, cy, cz), (cx + half_w, cy, cz), radius, radius, 8, tire_cell, team, bone)
            self.add_cylinder((cx - half_w * 1.08, cy, cz), (cx + half_w * 1.08, cy, cz), radius * 0.80, radius * 0.80, 8, wood_cell, team, bone)
            self.add_cylinder((cx - half_w * 1.28, cy, cz), (cx + half_w * 1.28, cy, cz), radius * 0.24, radius * 0.24, 6, tire_cell, team, bone)
            return

        # Chunky spoked wheel with tire rim blocks + hub + spokes for LOD0.
        rim_segs = max(8, spokes * 2)
        rim_r = radius
        inner_r = radius * 0.72
        for i in range(rim_segs):
            a0 = 2 * math.pi * i / rim_segs
            a1 = 2 * math.pi * (i + 1) / rim_segs
            amid = (a0 + a1) * 0.5
            seg_len = 2 * rim_r * math.sin(math.pi / rim_segs) * 1.05
            p = (cx, cy + math.cos(amid) * (rim_r + inner_r) * 0.5, cz + math.sin(amid) * (rim_r + inner_r) * 0.5)
            self.add_box(p, (width, rim_r - inner_r, seg_len), (math.degrees(amid), 0.0, 0.0), tire_cell, team, bone)
        # Hub
        self.add_cylinder((cx - half_w * 1.25, cy, cz), (cx + half_w * 1.25, cy, cz), radius * 0.24, radius * 0.24, max(6, spokes), tire_cell, team, bone)
        # Spokes
        for i in range(spokes):
            a = 2 * math.pi * i / spokes
            p0 = (cx, cy, cz)
            p1 = (cx, cy + math.cos(a) * inner_r, cz + math.sin(a) * inner_r)
            self.add_cylinder(p0, p1, radius * 0.07, radius * 0.06, 4, wood_cell, team, bone)

    def add_arc_tube(self, points, radius, segments, cell, team, bone):
        for i in range(len(points) - 1):
            r0 = radius[i] if isinstance(radius, (list, tuple)) else radius
            r1 = radius[i + 1] if isinstance(radius, (list, tuple)) else radius
            self.add_cylinder(points[i], points[i + 1], r0, r1, segments, cell, team, bone, cap_ends=True)

    def add_thin_plate(self, corners4, thickness, cell, team, bone):
        """Closed thin 4-corner slab (never culled from either side)."""
        pts = [Vector(c) for c in corners4]
        n = (pts[1] - pts[0]).cross(pts[3] - pts[0])
        if n.length < 1e-6:
            return
        offset = n.normalized() * (thickness * 0.5)
        front = [self.bm.verts.new(p + offset) for p in pts]
        back = [self.bm.verts.new(p - offset) for p in pts]
        quads = [
            (front[0], front[1], front[2], front[3]),
            (back[3], back[2], back[1], back[0]),
            (front[0], back[0], back[1], front[1]),
            (front[1], back[1], back[2], front[2]),
            (front[2], back[2], back[3], front[3]),
            (front[3], back[3], back[0], front[0]),
        ]
        faces = [self.bm.faces.new(q) for q in quads]
        self._tag_geom(front + back, faces, cell, team, bone)

    def finalize(self, mesh_name, rig, collection, material, min_tris, max_tris):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])

        # Safety net: deterministically subdivide largest faces with poke if below LOD min budget,
        # preserving exact silhouette, single-cell UVs, TeamMask and bone weights.
        while len(self.bm.faces) < min_tris and len(self.bm.faces) > 0:
            need = min_tris - len(self.bm.faces)
            # Each poke adds +2 triangles.
            batch_count = max(1, min(len(self.bm.faces), (need + 1) // 2))
            candidates = sorted(self.bm.faces, key=lambda f: f.calc_area(), reverse=True)[:batch_count]
            for f in candidates:
                code = f[self.cell_layer]
                team_flag = f[self.team_layer]
                bones = [self.vert_bone.get(v, 0) for v in f.verts]
                dominant_bone = max(set(bones), key=bones.count)
                res = bmesh.ops.poke(self.bm, faces=[f])
                for nv in res["verts"]:
                    nv[self.deform][dominant_bone] = 1.0
                    self.vert_bone[nv] = dominant_bone
                for nf in res["faces"]:
                    nf[self.cell_layer] = code
                    nf[self.team_layer] = team_flag

        uv_layer = self.bm.loops.layers.uv.new("UVMap")
        mask_layer = self.bm.loops.layers.color.new("TeamMask")
        for f in self.bm.faces:
            code = f[self.cell_layer]
            cx = code % self.grid
            cy = code // self.grid
            uv = ((cx + 0.5) / self.grid, (cy + 0.5) / self.grid)
            team = 1.0 if f[self.team_layer] else 0.0
            for loop in f.loops:
                loop[uv_layer].uv = uv
                loop[mask_layer] = (team, team, team, 1.0)

        self.bm.faces.layers.int.remove(self.cell_layer)
        self.bm.faces.layers.int.remove(self.team_layer)

        mesh = bpy.data.meshes.new(mesh_name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(material)

        obj = bpy.data.objects.new(mesh_name, mesh)
        collection.objects.link(obj)
        for bone_name in self.bone_names:
            obj.vertex_groups.new(name=bone_name)
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        obj.parent = rig

        tris = len(mesh.polygons)
        if tris > max_tris:
            raise RuntimeError(f"{mesh_name} exceeded max_tris ({tris} > {max_tris})")
        return obj, tris


def add_humanoid_base(b: UnitBuilder, lod: int, tunic_cell="team_tabard", tunic_team=1, pants_cell="cloth_brown",
                      skin_cell="skin", leather_cell="leather", tunic_skirt=True, shoulder_pads=False, metal_cell="metal"):
    seg = (10, 6, 4)[lod]
    ring = (8, 5, 3)[lod]
    arm_seg = (8, 5, 4)[lod]

    if lod == 2:
        # Ultra-low-poly silhouette blockout (< 100 tris total across whole unit).
        b.add_box((0.0, 0.0, 0.44), (0.34, 0.22, 0.88), (0, 0, 0), pants_cell, 0, "hips")
        b.add_tapered_box((0.0, 0.0, 1.18), (0.44, 0.26), (0.50, 0.28), 0.62, (0, 0, 0), tunic_cell, tunic_team, "chest")
        b.add_box((0.0, 0.0, 1.66), (0.28, 0.28, 0.30), (0, 0, 0), skin_cell, 0, "head")
        return

    # Boots + shins + thighs
    for side, sgn in (("L", 1.0), ("R", -1.0)):
        x = sgn * 0.115
        b.add_tapered_box((x, -0.04, 0.06), (0.14, 0.24), (0.13, 0.18), 0.12, (0, 0, 0), leather_cell, 0, f"shin.{side}")
        b.add_cylinder((x, 0.0, 0.12), (x, 0.0, 0.48), 0.075, 0.085, arm_seg, leather_cell if lod == 0 else pants_cell, 0, f"shin.{side}")
        b.add_cylinder((x, 0.0, 0.48), (x, 0.0, 0.90), 0.09, 0.105, arm_seg, pants_cell, 0, f"thigh.{side}")

    # Hips + belted tunic skirt
    b.add_cylinder((0.0, 0.0, 0.85), (0.0, 0.0, 1.06), 0.20, 0.19, seg, tunic_cell if tunic_skirt else pants_cell, tunic_team if tunic_skirt else 0, "hips")
    if tunic_skirt:
        b.add_cylinder((0.0, 0.0, 0.68), (0.0, 0.0, 0.88), 0.23, 0.20, seg, tunic_cell, tunic_team, "hips")

    # Torso (spine + chest)
    b.add_cylinder((0.0, 0.0, 1.04), (0.0, 0.0, 1.48), 0.21, 0.24, seg, tunic_cell, tunic_team, "chest")
    # Leather belt + brass buckle
    b.add_cylinder((0.0, 0.0, 0.92), (0.0, 0.0, 1.00), 0.215, 0.215, seg, leather_cell, 0, "hips")
    if lod == 0:
        b.add_box((0.0, -0.21, 0.96), (0.10, 0.04, 0.08), (0, 0, 0), "brass", 0, "hips")

    if shoulder_pads:
        for side, sgn in (("L", 1.0), ("R", -1.0)):
            b.add_sphere((sgn * 0.26, 0.0, 1.42), (0.11, 0.12, 0.09), seg, ring, metal_cell, 0, f"upper_arm.{side}")

    # Neck + head
    b.add_cylinder((0.0, 0.0, 1.47), (0.0, 0.0, 1.56), 0.08, 0.08, arm_seg, skin_cell, 0, "head")
    b.add_sphere((0.0, 0.0, 1.67), (0.14, 0.15, 0.16), seg, ring, skin_cell, 0, "head")
    if lod == 0:
        # Subtle toy-box nose mark for readable facing at hero distance
        b.add_box((0.0, -0.15, 1.66), (0.04, 0.04, 0.05), (0, 0, 0), skin_cell, 0, "head")


def add_humanoid_arms(b: UnitBuilder, lod: int, sleeve_cell="team_tabard", sleeve_team=1,
                      l_elbow=(0.30, -0.08, 1.16), l_hand=(0.32, -0.22, 1.04),
                      r_elbow=(-0.30, -0.08, 1.16), r_hand=(-0.30, -0.24, 1.06)):
    if lod == 2:
        return
    seg = (8, 5, 4)[lod]
    b.add_cylinder((0.24, 0.0, 1.42), l_elbow, 0.075, 0.065, seg, sleeve_cell, sleeve_team, "upper_arm.L")
    b.add_cylinder(l_elbow, l_hand, 0.065, 0.055, seg, sleeve_cell, sleeve_team, "forearm.L")
    b.add_sphere(l_hand, (0.06, 0.06, 0.06), seg, 4, "skin", 0, "forearm.L")

    b.add_cylinder((-0.24, 0.0, 1.42), r_elbow, 0.075, 0.065, seg, sleeve_cell, sleeve_team, "upper_arm.R")
    b.add_cylinder(r_elbow, r_hand, 0.065, 0.055, seg, sleeve_cell, sleeve_team, "forearm.R")
    b.add_sphere(r_hand, (0.06, 0.06, 0.06), seg, 4, "skin", 0, "forearm.R")


def build_infantry(b: UnitBuilder, lod: int):
    add_humanoid_base(b, lod, shoulder_pads=(lod == 0))
    add_humanoid_arms(b, lod, l_elbow=(0.30, -0.12, 1.20), l_hand=(0.36, -0.22, 1.14),
                      r_elbow=(-0.29, -0.05, 1.18), r_hand=(-0.32, -0.16, 1.18))

    if lod == 2:
        # Kettle helmet
        b.add_tapered_box((0.0, 0.0, 1.84), (0.36, 0.36), (0.20, 0.20), 0.14, (0, 0, 0), "metal", 0, "head")
        # Big heater shield on left
        b.add_box((0.40, -0.14, 1.08), (0.08, 0.56, 0.82), (0, 0, 0), "team_tabard", 1, "forearm.L")
        # Upright spear on right
        b.add_box((-0.34, -0.14, 1.28), (0.07, 0.07, 2.50), (0, 0, 0), "wood", 0, "forearm.R")
        b.add_tapered_box((-0.34, -0.14, 2.62), (0.10, 0.10), (0.01, 0.01), 0.24, (0, 0, 0), "metal", 0, "forearm.R")
        return

    seg = (12, 6, 4)[lod]
    # Kettle / nasal helmet with team crest
    b.add_sphere((0.0, 0.0, 1.72), (0.16, 0.17, 0.14), seg, 5, "metal", 0, "head")
    b.add_cylinder((0.0, 0.0, 1.69), (0.0, 0.0, 1.74), 0.21, 0.18, seg, "metal", 0, "head")
    b.add_box((0.0, 0.0, 1.86), (0.06, 0.24, 0.10), (0, 0, 0), "team_tabard", 1, "head")

    # Large heater shield on left arm (big team-colour area)
    b.add_tapered_box((0.40, -0.18, 1.06), (0.06, 0.42), (0.06, 0.58), 0.78, (0, 0, -12), "team_tabard", 1, "forearm.L")
    b.add_sphere((0.44, -0.18, 1.12), (0.05, 0.09, 0.09), 8, 4, "metal", 0, "forearm.L")
    if lod == 0:
        # Shield rim bars
        b.add_box((0.42, -0.18, 1.45), (0.07, 0.60, 0.05), (0, 0, -12), "metal", 0, "forearm.L")

    # Long spear in right hand
    b.add_cylinder((-0.34, -0.18, 0.08), (-0.34, -0.22, 2.38), 0.032, 0.030, 6 if lod == 0 else 4, "wood", 0, "forearm.R")
    b.add_cylinder((-0.34, -0.22, 2.38), (-0.34, -0.22, 2.68), 0.065, 0.005, 6 if lod == 0 else 4, "metal", 0, "forearm.R")


def build_cavalry(b: UnitBuilder, lod: int):
    if lod == 2:
        # Simplified horse + rider + tall lance & plume (< 100 tris)
        b.add_box((0.0, 0.0, 1.10), (0.56, 1.46, 0.56), (0, 0, 0), "horse_bay", 0, "spine")
        b.add_box((0.0, 0.0, 1.08), (0.62, 0.64, 0.58), (0, 0, 0), "team_tabard", 1, "spine")
        for sx in (-0.18, 0.18):
            b.add_cylinder((sx, -0.50, 0.90), (sx, -0.50, 0.0), 0.10, 0.08, 3, "horse_bay", 0, "front_thigh.L" if sx > 0 else "front_thigh.R", cap_ends=False)
            b.add_cylinder((sx, 0.50, 0.90), (sx, 0.50, 0.0), 0.11, 0.08, 3, "horse_bay", 0, "hind_thigh.L" if sx > 0 else "hind_thigh.R", cap_ends=False)
        b.add_cylinder((0.0, -0.56, 1.24), (0.0, -0.92, 1.66), 0.20, 0.14, 3, "horse_bay", 0, "neck")
        b.add_cylinder((0.0, -0.84, 1.66), (0.0, -1.16, 1.44), 0.14, 0.09, 3, "horse_bay", 0, "head")
        b.add_box((0.0, 0.0, 1.62), (0.42, 0.32, 0.58), (0, 0, 0), "team_tabard", 1, "rider_torso")
        b.add_cylinder((0.0, 0.0, 1.92), (0.0, 0.0, 2.52), 0.16, 0.08, 3, "team_tabard", 1, "rider_head")
        b.add_cylinder((-0.34, 0.20, 1.15), (-0.34, -0.85, 3.05), 0.045, 0.025, 3, "wood", 0, "rider_arm.R", cap_ends=False)
        return

    seg = (10, 6, 4)[lod]
    ring = (6, 4, 3)[lod]
    # Horse body (pelvis, barrel, chest)
    b.add_sphere((0.0, 0.45, 1.06), (0.27, 0.32, 0.29), seg, ring, "horse_bay", 0, "pelvis")
    b.add_cylinder((0.0, 0.45, 1.06), (0.0, -0.52, 1.12), 0.28, 0.29, seg, "horse_bay", 0, "spine")
    b.add_sphere((0.0, -0.52, 1.12), (0.28, 0.30, 0.30), seg, ring, "horse_bay", 0, "chest")

    # Horse neck, head, ears, mane, tail
    b.add_cylinder((0.0, -0.56, 1.18), (0.0, -0.86, 1.60), 0.22, 0.14, seg, "horse_bay", 0, "neck")
    b.add_cylinder((0.0, -0.82, 1.62), (0.0, -1.16, 1.42), 0.14, 0.09, seg, "horse_bay", 0, "head")
    b.add_box((0.0, -0.66, 1.46), (0.08, 0.14, 0.46), (34, 0, 0), "horse_mane", 0, "neck")
    if lod == 0:
        for sgn in (-1.0, 1.0):
            b.add_tapered_box((sgn * 0.08, -0.82, 1.72), (0.05, 0.06), (0.01, 0.02), 0.12, (0, 0, 0), "horse_bay", 0, "head")
    b.add_arc_tube([(0.0, 0.68, 1.14), (0.0, 0.86, 0.92), (0.0, 0.94, 0.56)], [0.07, 0.08, 0.04], 6, "horse_mane", 0, "tail")

    # 4 horse legs + hooves
    leg_seg = 6 if lod == 0 else 4
    for side, sx in (("L", 1.0), ("R", -1.0)):
        x = sx * 0.18
        b.add_cylinder((x, -0.46, 1.04), (x, -0.46, 0.54), 0.10, 0.07, leg_seg, "horse_bay", 0, f"front_thigh.{side}")
        b.add_cylinder((x, -0.46, 0.54), (x, -0.46, 0.09), 0.065, 0.055, leg_seg, "horse_bay", 0, f"front_shin.{side}")
        b.add_cylinder((x, -0.46, 0.09), (x, -0.46, 0.0), 0.065, 0.075, leg_seg, "horse_mane", 0, f"front_shin.{side}")

        b.add_cylinder((x, 0.48, 1.04), (x, 0.50, 0.54), 0.12, 0.08, leg_seg, "horse_bay", 0, f"hind_thigh.{side}")
        b.add_cylinder((x, 0.50, 0.54), (x, 0.50, 0.09), 0.07, 0.055, leg_seg, "horse_bay", 0, f"hind_shin.{side}")
        b.add_cylinder((x, 0.50, 0.09), (x, 0.50, 0.0), 0.065, 0.075, leg_seg, "horse_mane", 0, f"hind_shin.{side}")

    # Team caparison/saddlecloth + leather saddle
    b.add_tapered_box((0.0, 0.0, 1.12), (0.64, 0.82), (0.58, 0.72), 0.42, (0, 0, 0), "team_tabard", 1, "spine")
    b.add_box((0.0, 0.0, 1.38), (0.38, 0.46, 0.12), (0, 0, 0), "leather", 0, "spine")

    # Rider legs straddling saddle
    for sx in (1.0, -1.0):
        b.add_cylinder((sx * 0.27, -0.02, 1.34), (sx * 0.33, -0.10, 0.90), 0.08, 0.065, 5, "leather", 0, "rider_torso")

    # Rider torso, shoulders, plumed helmet
    b.add_cylinder((0.0, 0.0, 1.38), (0.0, 0.0, 1.86), 0.19, 0.22, seg, "team_tabard", 1, "rider_torso")
    b.add_cylinder((0.0, 0.0, 1.86), (0.0, 0.0, 1.94), 0.08, 0.08, 5, "skin", 0, "rider_head")
    b.add_sphere((0.0, 0.0, 2.06), (0.14, 0.15, 0.16), seg, ring, "metal", 0, "rider_head")
    b.add_box((0.0, 0.02, 2.24), (0.08, 0.24, 0.16), (-12, 0, 0), "team_tabard", 1, "rider_head")
    b.add_box((0.0, 0.18, 2.16), (0.07, 0.18, 0.22), (20, 0, 0), "team_tabard", 1, "rider_head")

    # Left rider arm + cavalry shield
    b.add_cylinder((0.22, 0.0, 1.80), (0.32, -0.18, 1.54), 0.065, 0.055, 5, "metal", 0, "rider_torso")
    b.add_tapered_box((0.36, -0.18, 1.52), (0.05, 0.34), (0.05, 0.46), 0.62, (0, 0, -10), "team_tabard", 1, "rider_torso")

    # Right rider arm + couched/angled lance with team pennant
    b.add_cylinder((-0.22, 0.0, 1.80), (-0.30, -0.20, 1.54), 0.065, 0.055, 5, "metal", 0, "rider_arm.R")
    b.add_cylinder((-0.34, 0.35, 1.15), (-0.34, -1.05, 2.95), 0.032, 0.026, 6 if lod == 0 else 4, "wood", 0, "rider_arm.R")
    b.add_cylinder((-0.34, -1.05, 2.95), (-0.34, -1.19, 3.13), 0.055, 0.004, 6 if lod == 0 else 4, "metal", 0, "rider_arm.R")
    b.add_thin_plate([
        (-0.34, -0.82, 2.66), (-0.34, -1.00, 2.89), (-0.34, -0.60, 2.75), (-0.34, -0.58, 2.60)
    ], 0.025, "team_tabard", 1, "rider_arm.R")


def build_ranged(b: UnitBuilder, lod: int):
    add_humanoid_base(b, lod, tunic_skirt=False, shoulder_pads=False, pants_cell="cloth_brown")
    add_humanoid_arms(b, lod, l_elbow=(0.24, -0.22, 1.38), l_hand=(0.14, -0.44, 1.38),
                      r_elbow=(-0.28, -0.10, 1.36), r_hand=(-0.10, -0.26, 1.40))

    if lod == 2:
        # Hooded cap + feather
        b.add_tapered_box((0.0, 0.04, 1.82), (0.30, 0.32), (0.12, 0.14), 0.22, (12, 0, 0), "cloth_brown", 0, "head")
        # Quiver on back
        b.add_box((0.12, 0.22, 1.25), (0.14, 0.14, 0.56), (0, -20, 0), "leather", 0, "chest")
        # Longbow held forward
        b.add_box((0.14, -0.46, 1.34), (0.08, 0.18, 1.36), (0, 0, 0), "wood", 0, "forearm.L")
        return

    seg = (10, 6, 4)[lod]
    # Feathered archer hood/cap
    b.add_sphere((0.0, 0.02, 1.72), (0.15, 0.17, 0.15), seg, 4, "cloth_brown", 0, "head")
    b.add_cylinder((0.0, 0.06, 1.76), (0.0, 0.22, 1.86), 0.13, 0.04, 6, "cloth_brown", 0, "head")
    b.add_box((0.10, 0.02, 1.86), (0.03, 0.18, 0.14), (18, 0, -15), "team_tabard", 1, "head")

    # Back quiver + fletched arrows
    b.add_cylinder((0.12, 0.22, 1.00), (-0.06, 0.22, 1.52), 0.075, 0.085, 6, "leather", 0, "chest")
    for off_x, off_y in ((-0.03, 0.02), (0.02, -0.02), (0.0, 0.03)):
        b.add_cylinder((-0.04 + off_x, 0.22 + off_y, 1.46), (-0.09 + off_x, 0.22 + off_y, 1.66), 0.014, 0.014, 4, "wood", 0, "chest")
        b.add_box((-0.09 + off_x, 0.22 + off_y, 1.65), (0.03, 0.05, 0.06), (0, 0, 0), "cloth_cream", 0, "chest")

    # Curved longbow held out in left hand
    bow_pts = [
        (0.14, -0.34, 2.02),
        (0.14, -0.43, 1.74),
        (0.14, -0.46, 1.38),
        (0.14, -0.43, 1.02),
        (0.14, -0.34, 0.74),
    ]
    b.add_arc_tube(bow_pts, [0.018, 0.026, 0.032, 0.026, 0.018], 6 if lod == 0 else 4, "wood", 0, "forearm.L")
    # Bowstring + nocked arrow
    if lod == 0:
        b.add_cylinder((0.14, -0.34, 2.00), (-0.08, -0.26, 1.38), 0.008, 0.008, 4, "cloth_cream", 0, "forearm.L")
        b.add_cylinder((-0.08, -0.26, 1.38), (0.14, -0.34, 0.76), 0.008, 0.008, 4, "cloth_cream", 0, "forearm.L")
    b.add_cylinder((-0.08, -0.26, 1.39), (0.14, -0.68, 1.39), 0.016, 0.014, 4, "wood", 0, "forearm.L")
    b.add_cylinder((0.14, -0.68, 1.39), (0.14, -0.78, 1.39), 0.032, 0.004, 4, "metal", 0, "forearm.L")


def build_siege(b: UnitBuilder, lod: int):
    if lod == 2:
        # Simplified wheeled mangonel silhouette (< 100 tris)
        b.add_box((0.0, 0.0, 0.58), (1.40, 2.40, 0.34), (0, 0, 0), "wood", 0, "frame")
        b.add_box((0.0, 0.0, 0.58), (1.56, 1.30, 0.40), (0, 0, 0), "team_tabard", 1, "frame")
        b.add_cylinder((-0.68, -0.15, 1.15), (0.68, -0.15, 1.15), 0.45, 0.45, 3, "wood", 0, "frame", cap_ends=False)
        for wx, wy, wn in ((0.86, -0.85, "wheel_fl"), (-0.86, -0.85, "wheel_fr"), (0.86, 0.85, "wheel_bl"), (-0.86, 0.85, "wheel_br")):
            b.add_box((wx, wy, 0.44), (0.20, 0.84, 0.84), (0, 0, 0), "wood", 0, wn)
        b.add_cylinder((0.0, -0.10, 0.90), (0.0, 0.85, 2.15), 0.12, 0.09, 3, "wood", 0, "arm", cap_ends=False)
        b.add_cylinder((0.0, 0.86, 2.02), (0.0, 0.86, 2.46), 0.28, 0.14, 3, "stone", 0, "arm")
        return

    # 4 heavy iron-rimmed wheels
    wheel_r = 0.48
    spokes = 6
    for wx, wy, wn in (
        (0.95, -0.85, "wheel_fl"),
        (-0.95, -0.85, "wheel_fr"),
        (0.95, 0.85, "wheel_bl"),
        (-0.95, 0.85, "wheel_br"),
    ):
        b.add_wheel((wx, wy, wheel_r), 0.20, wheel_r, spokes, "metal", "wood", 0, wn, simple=(lod == 1))

    # Axles
    b.add_cylinder((-1.05, -0.85, wheel_r), (1.05, -0.85, wheel_r), 0.08, 0.08, 6, "wood", 0, "frame")
    b.add_cylinder((-1.05, 0.85, wheel_r), (1.05, 0.85, wheel_r), 0.08, 0.08, 6, "wood", 0, "frame")

    # Main rectangular timber chassis
    for sx in (0.65, -0.65):
        b.add_box((sx, 0.0, 0.62), (0.20, 2.50, 0.22), (0, 0, 0), "wood", 0, "frame")
    for cy in (-1.05, -0.25, 0.65, 1.10):
        b.add_box((0.0, cy, 0.62), (1.40, 0.18, 0.18), (0, 0, 0), "wood", 0, "frame")

    # Upright A-frame stanchions + crossbeam
    for sx in (0.65, -0.65):
        b.add_cylinder((sx, -0.65, 0.68), (sx, -0.15, 1.65), 0.10, 0.09, 4, "wood", 0, "frame")
        b.add_cylinder((sx, 0.45, 0.68), (sx, -0.15, 1.65), 0.10, 0.09, 4, "wood", 0, "frame")
    b.add_box((0.0, -0.15, 1.62), (1.50, 0.22, 0.22), (0, 0, 0), "wood", 0, "frame")

    # Torsion rope bundle
    b.add_cylinder((-0.72, -0.10, 0.90), (0.72, -0.10, 0.90), 0.14, 0.14, 8 if lod == 0 else 6, "straw", 0, "frame")

    # Team side drapes / banners on chassis sides for clear strategic-zoom team read
    for sx in (0.78, -0.78):
        b.add_thin_plate([
            (sx, -0.75, 0.72), (sx, 0.75, 0.72), (sx, 0.65, 0.28), (sx, -0.65, 0.28)
        ], 0.04, "team_tabard", 1, "frame")

    # Throwing arm + cup + boulder
    b.add_cylinder((0.0, -0.10, 0.95), (0.0, 0.85, 2.15), 0.11, 0.08, 6 if lod == 0 else 4, "wood", 0, "arm")
    b.add_box((0.0, 0.85, 2.12), (0.48, 0.48, 0.18), (40, 0, 0), "metal", 0, "arm")
    b.add_sphere((0.0, 0.86, 2.28), (0.24, 0.24, 0.24), 8 if lod == 0 else 6, 5 if lod == 0 else 4, "stone", 0, "arm")


def build_spademen(b: UnitBuilder, lod: int):
    add_humanoid_base(b, lod, tunic_skirt=False, shoulder_pads=False, pants_cell="cloth_brown")
    add_humanoid_arms(b, lod, l_elbow=(0.24, -0.18, 1.18), l_hand=(0.14, -0.32, 1.24),
                      r_elbow=(-0.28, -0.12, 1.16), r_hand=(-0.18, -0.28, 1.02))

    if lod == 2:
        # Wide iron hat + leather apron + broad spade
        b.add_tapered_box((0.0, 0.0, 1.82), (0.44, 0.44), (0.20, 0.20), 0.14, (0, 0, 0), "metal", 0, "head")
        b.add_box((-0.10, -0.30, 1.12), (0.08, 0.08, 1.50), (0, -25, 0), "wood", 0, "forearm.R")
        b.add_box((-0.34, -0.30, 1.72), (0.36, 0.08, 0.42), (0, -25, 0), "metal", 0, "forearm.R")
        return

    seg = (12, 6, 4)[lod]
    # Wide-brimmed pioneer kettle hat
    b.add_sphere((0.0, 0.0, 1.72), (0.15, 0.16, 0.14), seg, 4, "metal", 0, "head")
    b.add_cylinder((0.0, 0.0, 1.68), (0.0, 0.0, 1.72), 0.25, 0.22, seg, "metal", 0, "head")

    # Leather pioneer apron over team tunic
    b.add_tapered_box((0.0, -0.18, 1.00), (0.34, 0.08), (0.28, 0.08), 0.68, (0, 0, 0), "leather", 0, "hips")

    # Heavy two-handed digging spade held diagonally with steel blade up-left/right so silhouette pops
    p_bot = Vector((0.20, -0.34, 0.72))
    p_top = Vector((-0.30, -0.34, 1.74))
    b.add_cylinder(p_bot, p_top, 0.030, 0.028, 6 if lod == 0 else 4, "wood", 0, "forearm.R")
    # D-handle at bottom
    b.add_box((0.24, -0.34, 0.66), (0.14, 0.05, 0.12), (0, -25, 0), "wood", 0, "forearm.R")
    # Wide square steel spade blade at upper end
    b.add_tapered_box((-0.36, -0.34, 1.88), (0.30, 0.045), (0.24, 0.045), 0.36, (0, -26, 0), "metal", 0, "forearm.R")


def build_fascine_carriers(b: UnitBuilder, lod: int):
    add_humanoid_base(b, lod, tunic_skirt=False, shoulder_pads=False, pants_cell="cloth_brown")
    add_humanoid_arms(b, lod, l_elbow=(0.30, -0.05, 1.38), l_hand=(0.24, 0.10, 1.56),
                      r_elbow=(-0.30, -0.05, 1.38), r_hand=(-0.24, 0.10, 1.56))

    if lod == 2:
        # Simple cap + HUGE "walking haystack" brushwood fascine bundle
        b.add_box((0.0, 0.0, 1.82), (0.28, 0.28, 0.14), (0, 0, 0), "metal", 0, "head")
        b.add_box((0.0, 0.22, 1.46), (1.16, 0.58, 0.58), (0, 0, 0), "straw", 0, "chest")
        return

    seg = (12, 6, 4)[lod]
    # Simple steel pot helmet
    b.add_sphere((0.0, 0.0, 1.72), (0.15, 0.16, 0.14), seg, 4, "metal", 0, "head")

    # Giant bound brushwood fascine cylinder slung across upper back/shoulders ("walking haystack")
    b_seg = 10 if lod == 0 else 6
    b.add_cylinder((-0.62, 0.24, 1.44), (0.62, 0.24, 1.44), 0.32, 0.32, b_seg, "straw", 0, "chest")
    # Recessed dark wood core caps at bundle ends + leather binding bands
    b.add_cylinder((-0.63, 0.24, 1.44), (-0.61, 0.24, 1.44), 0.26, 0.26, b_seg, "wood", 0, "chest")
    b.add_cylinder((0.61, 0.24, 1.44), (0.63, 0.24, 1.44), 0.26, 0.26, b_seg, "wood", 0, "chest")
    for bx in (-0.36, 0.0, 0.36):
        b.add_cylinder((bx - 0.03, 0.24, 1.44), (bx + 0.03, 0.24, 1.44), 0.335, 0.335, b_seg, "leather", 0, "chest")
    if lod == 0:
        # Shoulder straps & protruding sticks for organic haystack silhouette
        for sx in (-0.16, 0.16):
            b.add_box((sx, -0.02, 1.24), (0.06, 0.42, 0.44), (0, 0, 0), "leather", 0, "chest")
        for dx, dy, dz in ((-0.70, 0.30, 1.52), (0.72, 0.18, 1.38), (-0.68, 0.14, 1.35), (0.69, 0.32, 1.54)):
            b.add_cylinder((0.0, 0.24, 1.44), (dx, dy, dz), 0.03, 0.02, 4, "wood", 0, "chest")


def build_engineers(b: UnitBuilder, lod: int):
    add_humanoid_base(b, lod, tunic_skirt=False, shoulder_pads=False, pants_cell="cloth_brown")
    add_humanoid_arms(b, lod, l_elbow=(0.30, -0.02, 1.32), l_hand=(0.28, -0.10, 1.54),
                      r_elbow=(-0.28, -0.16, 1.20), r_hand=(-0.24, -0.32, 1.12))

    if lod == 2:
        # Flat-topped engineer helmet + timber beam on left shoulder + mallet in right hand
        b.add_box((0.0, 0.0, 1.82), (0.32, 0.32, 0.16), (0, 0, 0), "metal", 0, "head")
        b.add_box((0.28, 0.0, 1.58), (0.20, 1.45, 0.20), (15, 0, 0), "wood", 0, "chest")
        b.add_box((-0.28, -0.34, 1.26), (0.26, 0.22, 0.22), (0, 0, 0), "wood", 0, "forearm.R")
        return

    seg = (10, 6, 4)[lod]
    # Flat-topped helmet with brass band
    b.add_cylinder((0.0, 0.0, 1.68), (0.0, 0.0, 1.84), 0.16, 0.15, seg, "metal", 0, "head")
    b.add_cylinder((0.0, 0.0, 1.68), (0.0, 0.0, 1.73), 0.17, 0.17, seg, "brass", 0, "head")

    # Pocketed leather tool vest over team tunic
    b.add_tapered_box((0.0, 0.0, 1.24), (0.42, 0.28), (0.46, 0.30), 0.38, (0, 0, 0), "leather", 0, "chest")

    # Timber trestle beam carried on left shoulder
    b.add_box((0.28, 0.05, 1.58), (0.16, 1.50, 0.16), (-14, 0, 0), "wood", 0, "chest")
    if lod == 0:
        b.add_box((0.28, -0.50, 1.71), (0.18, 0.12, 0.18), (-14, 0, 0), "metal", 0, "chest")
        b.add_box((0.28, 0.55, 1.45), (0.18, 0.12, 0.18), (-14, 0, 0), "metal", 0, "chest")

    # Heavy carpenter's mallet held upright in right hand
    b.add_cylinder((-0.26, -0.34, 0.85), (-0.26, -0.34, 1.65), 0.030, 0.030, 5, "wood", 0, "forearm.R")
    b.add_cylinder((-0.40, -0.34, 1.66), (-0.12, -0.34, 1.66), 0.11, 0.11, 6, "wood", 0, "forearm.R")
    b.add_cylinder((-0.41, -0.34, 1.66), (-0.37, -0.34, 1.66), 0.115, 0.115, 6, "metal", 0, "forearm.R")


def build_pontoon_train(b: UnitBuilder, lod: int):
    if lod == 2:
        # 4-wheeled wagon + flat-bottomed boat hull + team drape + pennant (< 100 tris)
        b.add_box((0.0, 0.0, 0.56), (1.46, 2.30, 0.34), (0, 0, 0), "team_tabard", 1, "frame")
        for wx, wy, wn in ((0.80, -0.82, "wheel_fl"), (-0.80, -0.82, "wheel_fr"), (0.80, 0.82, "wheel_bl"), (-0.80, 0.82, "wheel_br")):
            b.add_box((wx, wy, 0.42), (0.18, 0.78, 0.78), (0, 0, 0), "wood", 0, wn)
        b.add_tapered_box((0.0, 0.0, 0.98), (1.00, 1.85), (1.36, 2.55), 0.48, (0, 0, 0), "wood", 0, "frame")
        b.add_cylinder((0.0, 0.65, 1.25), (0.0, 1.10, 2.10), 0.06, 0.26, 3, "team_tabard", 1, "arm")
        return

    wheel_r = 0.44
    spokes = 6
    for wx, wy, wn in (
        (0.84, -0.82, "wheel_fl"),
        (-0.84, -0.82, "wheel_fr"),
        (0.84, 0.82, "wheel_bl"),
        (-0.84, 0.82, "wheel_br"),
    ):
        b.add_wheel((wx, wy, wheel_r), 0.16, wheel_r, spokes, "metal", "wood", 0, wn, simple=(lod == 1))

    # Axles + wagon flatbed
    b.add_cylinder((-0.92, -0.82, wheel_r), (0.92, -0.82, wheel_r), 0.07, 0.07, 5, "wood", 0, "frame")
    b.add_cylinder((-0.92, 0.82, wheel_r), (0.92, 0.82, wheel_r), 0.07, 0.07, 5, "wood", 0, "frame")
    b.add_box((0.0, 0.0, 0.58), (1.34, 2.40, 0.16), (0, 0, 0), "wood", 0, "frame")

    # Team drapes along wagon sides
    for sx in (0.72, -0.72):
        b.add_thin_plate([
            (sx, -0.95, 0.66), (sx, 0.95, 0.66), (sx, 0.85, 0.32), (sx, -0.85, 0.32)
        ], 0.04, "team_tabard", 1, "frame")

    # Flat-bottomed wooden pontoon boat resting inverted/cradled on the wagon (distinct tapered bow & stern)
    # Bottom (top when inverted? Let's place it upright in cradles so the hollow boat + oars + stacked deck planks read!)
    b.add_tapered_box((0.0, 0.0, 0.86), (1.00, 1.85), (1.34, 2.55), 0.42, (0, 0, 0), "wood", 0, "frame")
    # Bow & stern angled prows
    b.add_tapered_box((0.0, -1.36, 0.96), (0.80, 0.34), (0.14, 0.12), 0.42, (28, 0, 0), "wood", 0, "frame")
    b.add_tapered_box((0.0, 1.36, 0.96), (0.80, 0.34), (0.14, 0.12), 0.42, (-28, 0, 0), "wood", 0, "frame")
    # Stacked deck planks on top + ropes + long oars
    b.add_box((0.0, 0.0, 1.12), (1.04, 1.75, 0.14), (0, 0, 0), "cloth_cream", 0, "frame")
    for sy in (-0.55, 0.55):
        b.add_box((0.0, sy, 1.00), (1.40, 0.08, 0.34), (0, 0, 0), "straw", 0, "frame")
    for sx in (0.64, -0.64):
        b.add_cylinder((sx, -1.05, 1.14), (sx, 1.15, 1.14), 0.032, 0.032, 4, "wood", 0, "frame")
        b.add_box((sx, 1.25, 1.14), (0.16, 0.32, 0.04), (0, 0, 0), "wood", 0, "frame")

    # Short stern mast + team guidon pennant
    b.add_cylinder((0.0, 0.72, 1.05), (0.0, 0.72, 2.15), 0.035, 0.025, 5, "wood", 0, "arm")
    b.add_thin_plate([
        (0.0, 0.72, 2.10), (0.0, 1.40, 1.98), (0.0, 1.35, 1.65), (0.0, 0.72, 1.65)
    ], 0.03, "team_tabard", 1, "arm")


def build_crossing_marshals(b: UnitBuilder, lod: int):
    add_humanoid_base(b, lod, tunic_skirt=True, shoulder_pads=False, pants_cell="leather")
    add_humanoid_arms(b, lod, l_elbow=(0.30, -0.10, 1.24), l_hand=(0.24, -0.18, 1.12),
                      r_elbow=(-0.30, -0.12, 1.34), r_hand=(-0.34, -0.24, 1.48))

    if lod == 2:
        # Plumed marshal hat + tall guidon staff with twin-tailed team banner
        b.add_tapered_box((0.0, 0.0, 1.84), (0.48, 0.42), (0.24, 0.24), 0.18, (0, 0, 0), "leather", 0, "head")
        b.add_box((0.0, 0.05, 2.08), (0.10, 0.28, 0.24), (0, 0, 0), "team_tabard", 1, "head")
        b.add_box((-0.36, -0.24, 1.45), (0.08, 0.08, 2.85), (0, 0, 0), "wood", 0, "forearm.R")
        b.add_box((-0.36, 0.14, 2.55), (0.08, 0.72, 0.56), (0, 0, 0), "team_tabard", 1, "forearm.R")
        return

    seg = (12, 6, 4)[lod]
    # Brass gorget collar over team coat
    b.add_cylinder((0.0, 0.0, 1.42), (0.0, 0.0, 1.51), 0.24, 0.18, seg, "brass", 0, "chest")

    # Wide-brimmed marshal's plumed hat
    b.add_cylinder((0.0, 0.0, 1.72), (0.0, 0.0, 1.75), 0.28, 0.27, seg, "leather", 0, "head")
    b.add_cylinder((0.0, 0.0, 1.75), (0.0, 0.0, 1.90), 0.16, 0.14, seg, "leather", 0, "head")
    b.add_box((0.0, 0.02, 1.98), (0.08, 0.30, 0.18), (-12, 0, 0), "team_tabard", 1, "head")

    # Tall brass-tipped marshal's staff + large twin-tailed guidon flag
    b.add_cylinder((-0.35, -0.24, 0.02), (-0.35, -0.24, 2.85), 0.032, 0.026, 6 if lod == 0 else 4, "wood", 0, "forearm.R")
    b.add_cylinder((-0.35, -0.24, 2.85), (-0.35, -0.24, 3.04), 0.055, 0.004, 6 if lod == 0 else 4, "brass", 0, "forearm.R")
    # Horizontal crossbar
    b.add_cylinder((-0.35, -0.26, 2.74), (-0.35, 0.22, 2.74), 0.024, 0.020, 4, "brass", 0, "forearm.R")
    # Upper and lower swallowtail flag panels in team colour
    b.add_thin_plate([
        (-0.35, -0.22, 2.72), (-0.35, 0.42, 2.66), (-0.35, 0.24, 2.48), (-0.35, -0.22, 2.46)
    ], 0.03, "team_tabard", 1, "forearm.R")
    b.add_thin_plate([
        (-0.35, -0.22, 2.46), (-0.35, 0.24, 2.48), (-0.35, 0.42, 2.26), (-0.35, -0.22, 2.22)
    ], 0.03, "team_tabard", 1, "forearm.R")


UNIT_SPECS = [
    ("infantry", "U_infantry_a", HUMANOID_BONES, make_humanoid_anims, build_infantry, "Infantry foot soldier with heater shield, spear, kettle helmet and team tabard."),
    ("cavalry", "U_cavalry_a", QUADRUPED_BONES, make_quadruped_anims, build_cavalry, "Armoured warhorse and rider with shield, plumed helmet and pennanted lance."),
    ("ranged", "U_ranged_a", HUMANOID_BONES, make_humanoid_anims, build_ranged, "Archer with longbow, nocked arrow, back quiver and feathered hood."),
    ("siege", "U_siege_a", SIEGE_BONES, make_siege_anims, build_siege, "Wheeled timber stone-thrower (mangonel) with stone payload and team side-banners."),
    ("spademen", "U_spademen_a", HUMANOID_BONES, make_humanoid_anims, build_spademen, "Defender pioneer specialist with iron kettle hat, leather apron and heavy digging spade."),
    ("fascine-carriers", "U_fascinecarriers_a", HUMANOID_BONES, make_humanoid_anims, build_fascine_carriers, "Attacker specialist carrying a huge bound brushwood fascine bundle."),
    ("engineers", "U_engineers_a", HUMANOID_BONES, make_humanoid_anims, build_engineers, "Bridgework specialist with leather tool vest, timber beam on shoulder and carpenter's mallet."),
    ("pontoon-train", "U_pontoontrain_a", SIEGE_BONES, make_siege_anims, build_pontoon_train, "Four-wheeled wagon carrying a wooden pontoon boat, deck planks, oars and team pennant."),
    ("crossing-marshals", "U_crossingmarshals_a", HUMANOID_BONES, make_humanoid_anims, build_crossing_marshals, "Crossing marshal specialist with flared team coat, brass gorget, plumed hat and tall guidon staff."),
]

LOD_TARGETS = {
    0: (1050, 2450),
    1: (320, 580),
    2: (20, 96),
}


def build_unit(unit_id, asset_name, bones, anim_fn, builder_fn, summary, palette, palette_png_path):
    scene = reset_scene()
    rig_name = "RIG_humanoid" if bones is HUMANOID_BONES else ("RIG_quadruped" if bones is QUADRUPED_BONES else "RIG_siege")
    rig = make_rig(scene, rig_name, bones)
    material = make_palette_material(palette_png_path)
    bone_names = [b[0] for b in bones]

    counts = {}
    for lod in (0, 1, 2):
        col = bpy.data.collections.new(f"LOD{lod}")
        scene.collection.children.link(col)
        builder = UnitBuilder(palette, bone_names)
        builder_fn(builder, lod)
        min_t, max_t = LOD_TARGETS[lod]
        _, tris = builder.finalize(f"{asset_name}_LOD{lod}", rig, col, material, min_t, max_t)
        counts[f"LOD{lod}"] = tris

    anim_fn(rig, scene)

    blend_path = os.path.join(blender_cli.REPO_ROOT, "art", "source", f"{asset_name}.blend")
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)

    source_meta = {
        "name": asset_name,
        "unitType": unit_id,
        "source": "script",
        "id": asset_name,
        "url": "tools/blender/make_units.py",
        "author": "SmiteNot procedural generator",
        "licence": "CC0",
        "credit": "Generated by tools/blender/make_units.py",
        "changes": summary,
        "created": "2026-11-14",
    }
    blender_cli.write_json(os.path.join(blender_cli.REPO_ROOT, "art", "source", f"{asset_name}.source.json"), source_meta)
    print(f"make_units: {asset_name} ({unit_id}) -> LOD0={counts['LOD0']} LOD1={counts['LOD1']} LOD2={counts['LOD2']} -> {blend_path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--only", nargs="*", help="optional list of unit type ids to rebuild")
    args = blender_cli.parse_args(parser)

    palette = blender_cli.load_json("art/palettes/palette.json")
    palette_png_path = write_palette_png(palette)
    print(f"make_units: wrote {palette_png_path}")

    only = set(args.only) if args.only else None
    for unit_id, asset_name, bones, anim_fn, builder_fn, summary in UNIT_SPECS:
        if only and unit_id not in only:
            continue
        build_unit(unit_id, asset_name, bones, anim_fn, builder_fn, summary, palette, palette_png_path)


if __name__ == "__main__":
    main()
