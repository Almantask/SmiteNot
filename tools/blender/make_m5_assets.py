"""Build M5 Blender assets (crew, cattle, grove tree, felled trunk with crown, engine wreck, small fire patch).

Run from repository root:
  uv run --python 3.13 --with bpy python tools/blender/make_m5_assets.py

Generates art/palettes/palette.png and:
  - art/source/U_crew_a.blend (+ .source.json)
  - art/source/U_cattle_a.blend (+ .source.json)
  - art/source/P_tree_grove.blend (+ .source.json)
  - art/source/P_trunk_felled.blend (+ .source.json)
  - art/source/P_wreck_siege.blend (+ .source.json)
  - art/source/P_fire_small.blend (+ .source.json)
"""
import argparse
import math
import os
import sys

import bpy  # noqa: F401
import bmesh  # noqa: E402
from mathutils import Vector  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_cli  # noqa: E402
import make_units  # noqa: E402

LOD_TARGETS = make_units.LOD_TARGETS

CATTLE_BONES = [
    ("root", (0.0, 0.0, 0.0), (0.0, 0.4, 0.0), None),
    ("pelvis", (0.0, 0.48, 0.96), (0.0, 0.14, 0.99), "root"),
    ("spine", (0.0, 0.14, 0.99), (0.0, -0.26, 1.02), "pelvis"),
    ("chest", (0.0, -0.26, 1.02), (0.0, -0.58, 1.08), "spine"),
    ("neck", (0.0, -0.58, 1.08), (0.0, -0.82, 1.24), "chest"),
    ("head", (0.0, -0.82, 1.24), (0.0, -1.06, 1.08), "neck"),
    ("tail", (0.0, 0.64, 1.00), (0.0, 0.94, 0.56), "pelvis"),
    ("front_thigh.L", (0.20, -0.46, 0.94), (0.20, -0.46, 0.48), "chest"),
    ("front_shin.L", (0.20, -0.46, 0.48), (0.20, -0.46, 0.04), "front_thigh.L"),
    ("front_thigh.R", (-0.20, -0.46, 0.94), (-0.20, -0.46, 0.48), "chest"),
    ("front_shin.R", (-0.20, -0.46, 0.48), (-0.20, -0.46, 0.04), "front_thigh.R"),
    ("hind_thigh.L", (0.20, 0.50, 0.94), (0.20, 0.52, 0.48), "pelvis"),
    ("hind_shin.L", (0.20, 0.52, 0.48), (0.20, 0.52, 0.04), "hind_thigh.L"),
    ("hind_thigh.R", (-0.20, 0.50, 0.94), (-0.20, 0.52, 0.48), "pelvis"),
    ("hind_shin.R", (-0.20, 0.52, 0.48), (-0.20, 0.52, 0.04), "hind_thigh.R"),
]


class PropBuilder(make_units.UnitBuilder):
    """Low-poly builder for unrigged world props validated by tools/blender/validate.py."""

    def __init__(self, palette):
        super().__init__(palette, ["root"])

    def finalize_prop(self, mesh_name, collection, material, min_tris, max_tris):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        bmesh.ops.triangulate(self.bm, faces=self.bm.faces[:])

        while len(self.bm.faces) < min_tris and len(self.bm.faces) > 0:
            need = min_tris - len(self.bm.faces)
            batch = max(1, min(len(self.bm.faces), (need + 1) // 2))
            candidates = sorted(self.bm.faces, key=lambda f: f.calc_area(), reverse=True)[:batch]
            for f in candidates:
                code = f[self.cell_layer]
                res = bmesh.ops.poke(self.bm, faces=[f])
                for nf in res["faces"]:
                    nf[self.cell_layer] = code

        uv_layer = self.bm.loops.layers.uv.new("UVMap")
        for f in self.bm.faces:
            code = f[self.cell_layer]
            cx = code % self.grid
            cy = code // self.grid
            uv = ((cx + 0.5) / self.grid, (cy + 0.5) / self.grid)
            for loop in f.loops:
                loop[uv_layer].uv = uv

        self.bm.faces.layers.int.remove(self.cell_layer)
        self.bm.faces.layers.int.remove(self.team_layer)
        self.bm.verts.layers.deform.remove(self.deform)

        mesh = bpy.data.meshes.new(mesh_name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        mesh.materials.append(material)

        obj = bpy.data.objects.new(mesh_name, mesh)
        collection.objects.link(obj)

        tris = len(mesh.polygons)
        if tris > max_tris:
            raise RuntimeError(f"{mesh_name} exceeded max_tris ({tris} > {max_tris})")
        return obj, tris


def build_crew(b: make_units.UnitBuilder, lod: int):
    """Artillery & pontoon crewman: leather apron, baldric rope, padded engineer cap, heavy ramming handspike."""
    make_units.add_humanoid_base(
        b,
        lod,
        tunic_cell="team_tabard",
        tunic_team=1,
        pants_cell="cloth_brown",
        leather_cell="leather",
        tunic_skirt=False,
        shoulder_pads=False,
    )
    make_units.add_humanoid_arms(
        b,
        lod,
        sleeve_cell="team_tabard",
        sleeve_team=1,
        l_elbow=(0.30, -0.06, 1.16),
        l_hand=(0.28, -0.22, 1.05),
        r_elbow=(-0.31, -0.06, 1.18),
        r_hand=(-0.33, -0.20, 1.06),
    )

    seg = (10, 6, 4)[lod]
    if lod == 2:
        b.add_box((0.0, -0.14, 0.88), (0.38, 0.08, 0.56), (0, 0, 0), "leather", 0, "hips")
        b.add_tapered_box((0.0, 0.0, 1.80), (0.34, 0.34), (0.28, 0.28), 0.14, (0, 0, 0), "leather", 0, "head")
        b.add_box((-0.34, -0.18, 1.05), (0.08, 0.08, 1.85), (0, 0, 0), "wood", 0, "forearm.R")
        return

    # Heavy leather matross apron from chest down to knees.
    b.add_tapered_box((0.0, -0.225, 0.92), (0.34, 0.06), (0.26, 0.05), 0.72, (-3, 0, 0), "leather", 0, "hips")
    if lod == 0:
        b.add_tapered_box((0.0, -0.21, 1.32), (0.24, 0.04), (0.20, 0.04), 0.24, (2, 0, 0), "leather", 0, "chest")
        # Brass tool-belt clasp + hip pouch.
        b.add_box((0.20, -0.14, 0.94), (0.12, 0.16, 0.16), (0, 0, 10), "leather", 0, "hips")
        b.add_box((0.0, -0.25, 1.02), (0.11, 0.04, 0.09), (0, 0, 0), "brass", 0, "chest")

    # Padded leather engineer skullcap with rolled wool/cloth cuff + brass badge.
    b.add_cylinder((0.0, 0.0, 1.72), (0.0, 0.0, 1.77), 0.175, 0.175, seg, "cloth_cream", 0, "head")
    b.add_sphere((0.0, 0.0, 1.81), (0.155, 0.165, 0.13), seg, 6 if lod == 0 else 4, "leather", 0, "head")
    if lod == 0:
        b.add_box((0.0, -0.16, 1.78), (0.07, 0.04, 0.08), (0, 0, 0), "brass", 0, "head")
        # Coiled hemp rope sling over left shoulder + chest.
        b.add_arc_tube(
            [(-0.18, -0.18, 1.02), (0.0, -0.23, 1.24), (0.19, -0.14, 1.44), (0.18, 0.16, 1.40), (-0.16, 0.18, 1.06)],
            0.032,
            5,
            "straw",
            0,
            "chest",
        )
        # Carpenter's mallet tucked at belt.
        b.add_cylinder((0.24, 0.04, 0.72), (0.24, -0.02, 1.04), 0.024, 0.024, 5, "wood", 0, "hips")
        b.add_box((0.24, -0.03, 1.06), (0.10, 0.18, 0.10), (0, 0, 8), "wood", 0, "hips")

    # Heavy iron-shod wooden handspike / lever held upright in right hand.
    b.add_cylinder((-0.34, -0.20, 0.04), (-0.34, -0.20, 1.86), 0.036, 0.028, 6 if lod == 0 else 4, "wood", 0, "forearm.R")
    b.add_tapered_box((-0.34, -0.20, 1.94), (0.07, 0.07), (0.10, 0.03), 0.22, (0, 0, 0), "metal", 0, "forearm.R")


def build_drovers(b: make_units.UnitBuilder, lod: int):
    """Livestock drover specialist: wide-brimmed slouch hat, team tabard & shoulder mantle, goad staff, whip coil and horn."""
    make_units.add_humanoid_base(
        b,
        lod,
        tunic_cell="team_tabard",
        tunic_team=1,
        pants_cell="cloth_brown",
        leather_cell="leather",
        tunic_skirt=True,
        shoulder_pads=False,
    )
    make_units.add_humanoid_arms(
        b,
        lod,
        sleeve_cell="team_tabard",
        sleeve_team=1,
        l_elbow=(0.30, -0.08, 1.18),
        l_hand=(0.27, -0.22, 1.06),
        r_elbow=(-0.31, -0.06, 1.20),
        r_hand=(-0.33, -0.22, 1.08),
    )

    if lod == 2:
        # Wide-brimmed drover hat + team crown band + long goad crook
        b.add_box((0.0, -0.01, 1.75), (0.56, 0.56, 0.05), (-4, 0, 0), "leather", 0, "head")
        b.add_tapered_box((0.0, 0.0, 1.84), (0.30, 0.30), (0.24, 0.24), 0.14, (-4, 0, 0), "team_tabard", 1, "head")
        b.add_box((-0.34, -0.24, 1.06), (0.06, 0.06, 1.92), (8, 0, 0), "wood", 0, "forearm.R")
        b.add_box((-0.34, -0.44, 2.02), (0.06, 0.20, 0.08), (12, 0, 0), "metal", 0, "forearm.R")
        return

    seg = (10, 6, 4)[lod]
    # Wide-brimmed drover's slouch hat with team-colored hatband
    b.add_cylinder((0.0, -0.01, 1.71), (0.0, -0.02, 1.745), 0.31, 0.295, seg, "leather", 0, "head")
    b.add_cylinder((0.0, -0.01, 1.745), (0.0, -0.01, 1.79), 0.175, 0.165, seg, "team_tabard", 1, "head")
    b.add_cylinder((0.0, -0.01, 1.79), (0.0, 0.0, 1.92), 0.165, 0.135, seg, "leather", 0, "head")

    # Shoulder capelet / mantle and weathered coat tail
    b.add_cylinder((0.0, 0.0, 1.38), (0.0, 0.0, 1.49), 0.28, 0.20, seg, "team_tabard", 1, "chest")
    b.add_tapered_box((0.0, 0.19, 0.80), (0.42, 0.08), (0.34, 0.07), 0.56, (10, 0, 0), "cloth_brown", 0, "hips")

    # Long ashwood drover's goad staff with hooked iron crook in right hand
    b.add_cylinder((-0.33, -0.18, 0.04), (-0.33, -0.34, 1.96), 0.028, 0.024, 6 if lod == 0 else 4, "wood", 0, "forearm.R")
    crook_pts = [
        (-0.33, -0.34, 1.95),
        (-0.33, -0.41, 2.08),
        (-0.33, -0.52, 2.06),
        (-0.33, -0.53, 1.94),
    ]
    b.add_arc_tube(crook_pts if lod == 0 else crook_pts[:3], 0.024, 5 if lod == 0 else 3, "metal", 0, "forearm.R")

    # Coiled rawhide whip hanging at right hip / forearm & curved calling horn at left side
    if lod == 0:
        b.add_arc_tube(
            [(-0.32, -0.22, 1.06), (-0.38, -0.14, 0.92), (-0.31, -0.05, 0.84), (-0.25, -0.16, 0.96), (-0.32, -0.22, 1.06)],
            0.020,
            5,
            "straw",
            0,
            "forearm.R",
        )
        # Leather scrip pouch & cross-chest strap
        b.add_box((0.23, 0.03, 0.86), (0.11, 0.18, 0.20), (0, 0, -6), "leather", 0, "hips")
        b.add_arc_tube(
            [(-0.20, -0.19, 1.44), (0.02, -0.23, 1.20), (0.23, -0.06, 0.95), (0.20, 0.16, 1.18), (-0.18, 0.18, 1.42)],
            0.024,
            4,
            "leather",
            0,
            "chest",
        )
        # Brass-tipped cattle horn held in left hand / hip
        b.add_arc_tube(
            [(0.27, -0.22, 1.06), (0.28, -0.34, 0.96), (0.24, -0.42, 0.82), (0.18, -0.42, 0.72)],
            [0.022, 0.034, 0.052, 0.068],
            6,
            "horn_bone",
            0,
            "forearm.L",
        )
        b.add_cylinder((0.20, -0.42, 0.75), (0.17, -0.42, 0.70), 0.062, 0.072, 6, "brass", 0, "forearm.L")
    else:
        # Simplified horn & satchel for LOD1
        b.add_box((0.23, 0.02, 0.88), (0.10, 0.16, 0.18), (0, 0, 0), "leather", 0, "hips")
        b.add_cylinder((0.27, -0.22, 1.06), (0.21, -0.40, 0.76), 0.024, 0.064, 4, "horn_bone", 0, "forearm.L")


def make_cattle_anims(rig, scene):
    """Six distinct quadruped clips for U_cattle_a (walk, gore, kick, stampede, death, collapse)."""
    pulse = make_units._pulse

    def walk_frame(t):
        phase = 2 * math.pi * t
        s = math.radians(24) * math.sin(phase)
        rot = {
            "front_thigh.L": (s, 0.0, 0.0),
            "front_shin.L": (max(0.0, -math.sin(phase)) * math.radians(32), 0.0, 0.0),
            "hind_thigh.R": (s, 0.0, 0.0),
            "hind_shin.R": (max(0.0, -math.sin(phase)) * math.radians(30), 0.0, 0.0),
            "front_thigh.R": (-s, 0.0, 0.0),
            "front_shin.R": (max(0.0, math.sin(phase)) * math.radians(32), 0.0, 0.0),
            "hind_thigh.L": (-s, 0.0, 0.0),
            "hind_shin.L": (max(0.0, math.sin(phase)) * math.radians(30), 0.0, 0.0),
            "neck": (math.radians(8) * math.sin(phase), 0.0, math.radians(10) * math.cos(phase)),
            "head": (math.radians(-6) * math.cos(phase), 0.0, 0.0),
            "tail": (math.radians(10), math.radians(24) * math.sin(phase), 0.0),
        }
        loc = {"pelvis": (0.0, 0.0, -0.045 * abs(math.sin(phase * 2)))}
        return rot, loc

    def gore_frame(t):
        wind = pulse(t, 0.25, 6.0)
        hook = pulse(t, 0.56, 7.5)
        rot = {
            "pelvis": (math.radians(8) * hook - math.radians(6) * wind, 0.0, 0.0),
            "chest": (math.radians(10) * hook, math.radians(-16) * wind + math.radians(20) * hook, 0.0),
            "neck": (math.radians(38) * wind - math.radians(52) * hook, 0.0, math.radians(22) * hook),
            "head": (math.radians(22) * wind - math.radians(48) * hook, math.radians(-18) * wind, math.radians(28) * hook),
            "front_thigh.L": (math.radians(-38) * hook + math.radians(18) * wind, 0.0, 0.0),
            "front_shin.L": (math.radians(34) * wind, 0.0, 0.0),
            "front_thigh.R": (math.radians(24) * hook, 0.0, 0.0),
            "hind_thigh.L": (math.radians(22) * hook, 0.0, 0.0),
            "hind_thigh.R": (math.radians(20) * hook, 0.0, 0.0),
        }
        loc = {"pelvis": (0.0, 0.24 * hook - 0.10 * wind, -0.08 * hook)}
        return rot, loc

    def kick_frame(t):
        phase = 2 * math.pi * t
        kick = pulse(t, 0.45, 6.5)
        rot = {
            "pelvis": (math.radians(22) * kick, math.radians(10) * math.sin(phase), 0.0),
            "chest": (math.radians(-18) * kick, 0.0, 0.0),
            "neck": (math.radians(26) * kick, 0.0, 0.0),
            "front_thigh.L": (math.radians(-22) * kick, 0.0, 0.0),
            "front_thigh.R": (math.radians(-18) * kick, 0.0, 0.0),
            "hind_thigh.L": (math.radians(68) * kick, 0.0, math.radians(14) * kick),
            "hind_shin.L": (math.radians(-42) * kick, 0.0, 0.0),
            "hind_thigh.R": (math.radians(58) * pulse(t, 0.56, 7.0), 0.0, math.radians(-14) * kick),
            "hind_shin.R": (math.radians(-38) * pulse(t, 0.56, 7.0), 0.0, 0.0),
            "tail": (math.radians(-45) * kick, 0.0, 0.0),
        }
        loc = {"pelvis": (0.0, -0.14 * kick, -0.16 * kick)}
        return rot, loc

    def stampede_frame(t):
        phase = 2 * math.pi * t
        leap = max(0.0, math.sin(phase))
        push = max(0.0, -math.sin(phase))
        thrust = pulse(t, 0.48, 5.5)
        rot = {
            "pelvis": (math.radians(-14) * leap + math.radians(14) * push, 0.0, math.radians(8) * math.cos(phase)),
            "chest": (math.radians(10) * push, 0.0, 0.0),
            "neck": (math.radians(22) * leap, 0.0, 0.0),
            "head": (math.radians(-14) * leap + math.radians(12) * push, 0.0, 0.0),
            "front_thigh.L": (math.radians(-58) * leap + math.radians(34) * push, 0.0, 0.0),
            "front_shin.L": (math.radians(48) * push, 0.0, 0.0),
            "front_thigh.R": (math.radians(-48) * leap + math.radians(40) * push, 0.0, 0.0),
            "front_shin.R": (math.radians(52) * push, 0.0, 0.0),
            "hind_thigh.L": (math.radians(48) * leap - math.radians(34) * push, 0.0, 0.0),
            "hind_thigh.R": (math.radians(40) * leap - math.radians(40) * push, 0.0, 0.0),
            "tail": (math.radians(-35), math.radians(18) * math.sin(phase), 0.0),
        }
        loc = {"pelvis": (0.0, 0.28 * thrust, -0.22 * leap + 0.06 * push)}
        return rot, loc

    def death_frame(t):
        rear = pulse(t, 0.16, 7.0)
        fall = min(1.0, max(0.0, (t - 0.18) / 0.58))
        s = fall * fall * (3.0 - 2.0 * fall)
        rot = {
            "pelvis": (math.radians(-16) * rear - math.radians(8) * s, math.radians(84) * s, math.radians(10) * s),
            "chest": (math.radians(-10) * rear + math.radians(14) * s, math.radians(12) * s, 0.0),
            "neck": (math.radians(-18) * rear + math.radians(26) * s, math.radians(20) * s, 0.0),
            "head": (math.radians(16) * rear - math.radians(22) * s, 0.0, 0.0),
            "front_thigh.L": (math.radians(-42) * rear + math.radians(28) * s, 0.0, math.radians(22) * s),
            "front_shin.L": (math.radians(48) * rear + math.radians(36) * s, 0.0, 0.0),
            "front_thigh.R": (math.radians(-36) * rear - math.radians(28) * s, 0.0, math.radians(-26) * s),
            "front_shin.R": (math.radians(44) * rear + math.radians(24) * s, 0.0, 0.0),
            "hind_thigh.L": (math.radians(20) * rear + math.radians(24) * s, 0.0, math.radians(20) * s),
            "hind_shin.L": (math.radians(28) * s, 0.0, 0.0),
            "hind_thigh.R": (math.radians(18) * rear - math.radians(20) * s, 0.0, math.radians(-22) * s),
            "hind_shin.R": (math.radians(32) * s, 0.0, 0.0),
        }
        loc = {"pelvis": (0.20 * s, 0.12 * s, -0.18 * rear * (1.0 - s) + 0.72 * s)}
        return rot, loc

    def collapse_frame(t):
        buckle = pulse(t, 0.22, 6.5)
        fall = min(1.0, max(0.0, (t - 0.20) / 0.56))
        s = fall * fall * (3.0 - 2.0 * fall)
        rot = {
            "pelvis": (math.radians(26) * buckle + math.radians(10) * s, math.radians(-84) * s, math.radians(-12) * s),
            "chest": (math.radians(16) * buckle + math.radians(16) * s, math.radians(-12) * s, 0.0),
            "neck": (math.radians(22) * buckle + math.radians(20) * s, math.radians(-20) * s, 0.0),
            "head": (math.radians(-18) * s, 0.0, 0.0),
            "front_thigh.L": (math.radians(44) * buckle - math.radians(30) * s, 0.0, math.radians(24) * s),
            "front_shin.L": (math.radians(56) * buckle + math.radians(28) * s, 0.0, 0.0),
            "front_thigh.R": (math.radians(40) * buckle + math.radians(22) * s, 0.0, math.radians(-24) * s),
            "front_shin.R": (math.radians(54) * buckle + math.radians(30) * s, 0.0, 0.0),
            "hind_thigh.L": (math.radians(-24) * buckle - math.radians(22) * s, 0.0, math.radians(22) * s),
            "hind_shin.L": (math.radians(28) * s, 0.0, 0.0),
            "hind_thigh.R": (math.radians(-20) * buckle + math.radians(26) * s, 0.0, math.radians(-22) * s),
            "hind_shin.R": (math.radians(24) * s, 0.0, 0.0),
        }
        loc = {"pelvis": (-0.20 * s, 0.24 * s, 0.36 * buckle * (1.0 - s) + 0.74 * s)}
        return rot, loc

    walk_action = make_units._record_action(rig, scene, "A_quadruped_walk", 3, walk_frame)
    make_units._record_action(rig, scene, "A_quadruped_gore", 2, gore_frame)
    make_units._record_action(rig, scene, "A_quadruped_kick", 2, kick_frame)
    make_units._record_action(rig, scene, "A_quadruped_stampede", 2, stampede_frame)
    make_units._record_action(rig, scene, "A_quadruped_death", 2, death_frame)
    make_units._record_action(rig, scene, "A_quadruped_collapse", 2, collapse_frame)
    rig.animation_data.action = walk_action
    return walk_action


def build_cattle(b: make_units.UnitBuilder, lod: int):
    """Horned draught bull / range cow (~1.54 m tall to horn tips, neutral russet & brindle hide)."""
    seg = (10, 6, 4)[lod]
    ring = (8, 5, 3)[lod]

    if lod == 2:
        b.add_box((0.0, 0.0, 0.88), (0.62, 1.38, 0.64), (0, 0, 0), "cattle_hide", 0, "spine")
        b.add_box((0.0, -0.50, 0.90), (0.66, 0.22, 0.68), (0, 0, 0), "team_tabard", 1, "chest")
        b.add_box((0.0, -0.78, 1.10), (0.38, 0.44, 0.42), (10, 0, 0), "cattle_dark", 0, "head")
        b.add_box((0.0, -0.74, 1.49), (0.68, 0.09, 0.11), (0, 0, 0), "horn_bone", 0, "head")
        for lx, ly, bone in ((0.20, -0.46, "front_thigh.L"), (-0.20, -0.46, "front_thigh.R"),
                             (0.20, 0.50, "hind_thigh.L"), (-0.20, 0.50, "hind_thigh.R")):
            b.add_box((lx, ly, 0.36), (0.15, 0.15, 0.72), (0, 0, 0), "cattle_hide", 0, bone)
        return

    # Broad barrel belly & hindquarter + dark brindle shoulder withers & dewlap.
    b.add_sphere((0.0, 0.34, 0.94), (0.31, 0.38, 0.33), seg, ring, "cattle_hide", 0, "pelvis")
    b.add_sphere((0.0, -0.02, 0.96), (0.34, 0.42, 0.35), seg, ring, "cattle_hide", 0, "spine")
    b.add_sphere((0.0, -0.42, 1.06), (0.33, 0.36, 0.37), seg, ring, "cattle_dark", 0, "chest")
    # Hanging dewlap along brisket
    b.add_tapered_box((0.0, -0.58, 0.82), (0.12, 0.30), (0.16, 0.36), 0.32, (12, 0, 0), "cattle_hide", 0, "chest")
    # Heavy neck crest connecting shoulders to raised bull poll + team-tagged drover collar band
    b.add_cylinder((0.0, -0.52, 1.08), (0.0, -0.82, 1.24), 0.25, 0.21, seg, "cattle_dark", 0, "neck")
    b.add_cylinder((0.0, -0.60, 1.12), (0.0, -0.69, 1.17), 0.255, 0.245, seg, "team_tabard", 1, "neck")
    # Broad triangular cattle head & wet muzzle
    b.add_tapered_box((0.0, -0.90, 1.18), (0.20, 0.24), (0.32, 0.34), 0.36, (52, 0, 0), "cattle_hide", 0, "head")
    b.add_box((0.0, -1.04, 1.04), (0.19, 0.16, 0.16), (12, 0, 0), "cattle_dark", 0, "head")

    # Swept ivory bull horns rising past 1.52 m (ensuring clear silhouette & VAT height > 1.4 m).
    for side, sgn in (("L", 1.0), ("R", -1.0)):
        b.add_arc_tube(
            [
                (sgn * 0.14, -0.82, 1.28),
                (sgn * 0.30, -0.80, 1.38),
                (sgn * 0.36, -0.74, 1.52),
                (sgn * 0.29, -0.68, 1.56),
            ],
            [0.055, 0.042, 0.025, 0.010],
            5 if lod == 0 else 4,
            "horn_bone",
            0,
            "head",
        )
        # Floppy ears below horns
        b.add_box((sgn * 0.21, -0.78, 1.20), (0.14, 0.06, 0.08), (0, sgn * 18, 0), "cattle_dark", 0, "head")

    # Four sturdy stocky legs + dark cloven hooves
    leg_defs = [
        ((0.20, -0.46), "front_thigh.L", "front_shin.L"),
        ((-0.20, -0.46), "front_thigh.R", "front_shin.R"),
        ((0.20, 0.50), "hind_thigh.L", "hind_shin.L"),
        ((-0.20, 0.50), "hind_thigh.R", "hind_shin.R"),
    ]
    lseg = 6 if lod == 0 else 4
    for (lx, ly), thigh_b, shin_b in leg_defs:
        b.add_cylinder((lx, ly, 0.94), (lx, ly, 0.48), 0.115, 0.075, lseg, "cattle_hide", 0, thigh_b)
        b.add_cylinder((lx, ly, 0.48), (lx, ly, 0.09), 0.070, 0.058, lseg, "cattle_hide", 0, shin_b)
        b.add_cylinder((lx, ly, 0.09), (lx, ly, 0.00), 0.068, 0.076, lseg, "cattle_dark", 0, shin_b)

    # Rope halter strap around neck (LOD0 detail) & tufted tail
    b.add_cylinder((0.0, 0.62, 1.00), (0.0, 0.86, 0.62), 0.042, 0.028, 5, "cattle_hide", 0, "tail")
    b.add_sphere((0.0, 0.90, 0.54), (0.055, 0.055, 0.10), 5, 4, "cattle_dark", 0, "tail")
    if lod == 0:
        b.add_arc_tube(
            [(-0.22, -0.72, 1.15), (0.0, -0.78, 0.96), (0.22, -0.72, 1.15), (0.0, -0.68, 1.34), (-0.22, -0.72, 1.15)],
            0.026,
            5,
            "straw",
            0,
            "neck",
        )


def build_tree_grove(b: PropBuilder, lod: int):
    """14 m dry-grove deciduous tree: buttressed gnarled oak trunk, four spreading boughs, multi-lobe dry/mid canopy."""
    t_seg = (10, 6, 4)[lod]
    b_seg = (7, 5, 3)[lod]
    c_seg = (10, 6, 4)[lod]
    c_ring = (7, 4, 3)[lod]

    if lod == 2:
        b.add_cylinder((0.0, 0.0, 0.0), (0.0, 0.0, 7.6), 0.46, 0.24, 4, "bark_dark", 0, "root")
        b.add_sphere((0.0, 0.0, 10.6), (3.4, 3.3, 3.6), 5, 3, "foliage_dry", 0, "root")
        b.add_sphere((-1.5, 0.9, 10.1), (2.0, 2.0, 2.1), 4, 3, "foliage_mid", 0, "root")
        b.add_sphere((1.4, -1.0, 11.0), (1.9, 1.8, 2.0), 4, 3, "foliage_dark", 0, "root")
        return

    # Root buttresses anchored cleanly at z=0.
    for i in range(5 if lod == 0 else 3):
        a = 2 * math.pi * i / (5 if lod == 0 else 3)
        rx, ry = math.cos(a) * 0.95, math.sin(a) * 0.95
        b.add_cylinder((rx, ry, 0.0), (math.cos(a) * 0.22, math.sin(a) * 0.22, 1.65), 0.22, 0.16, b_seg, "bark_dark", 0, "root")

    # Twisted tapering main trunk up to fork at 6.5 m -> crown leader at 10.2 m.
    b.add_arc_tube(
        [(0.0, 0.0, 0.0), (0.08, -0.06, 2.6), (-0.10, 0.08, 5.2), (0.02, 0.0, 7.2), (0.12, -0.08, 9.8)],
        [0.44, 0.36, 0.30, 0.22, 0.12],
        t_seg,
        "bark_dark",
        0,
        "root",
    )

    # Spreading primary boughs
    boughs = [
        ((0.02, 0.0, 6.2), (2.2, 1.5, 9.8)),
        ((-0.05, 0.04, 5.8), (-2.4, 1.2, 9.5)),
        ((0.0, -0.02, 6.6), (-1.4, -2.3, 10.2)),
        ((0.05, 0.02, 6.9), (1.6, -1.9, 10.6)),
    ]
    for p0, p1 in boughs:
        b.add_cylinder(p0, p1, 0.18, 0.08, b_seg, "bark_dark", 0, "root")

    # Canopy clumps: blend dry amber, warm olive mid, and dark shaded inner greens (total ~14 m tall).
    crown_lobes = [
        ((0.0, 0.0, 11.4), (2.6, 2.5, 2.4), "foliage_mid"),
        ((0.3, -0.2, 12.6), (1.9, 1.8, 1.6), "foliage_dry"),
        ((2.2, 1.4, 10.2), (2.1, 2.0, 1.8), "foliage_dry"),
        ((-2.3, 1.1, 10.0), (2.2, 1.9, 1.9), "foliage_mid"),
        ((-1.4, -2.1, 10.6), (2.0, 2.1, 1.8), "foliage_dry"),
        ((1.6, -1.8, 10.8), (1.9, 1.8, 1.8), "foliage_dark"),
        ((0.1, 1.9, 9.4), (1.7, 1.7, 1.4), "foliage_dark"),
        ((-0.8, 0.4, 8.9), (1.8, 1.6, 1.3), "foliage_dark"),
    ]
    for idx, (ctr, rad, cell) in enumerate(crown_lobes):
        if lod == 1 and idx >= 5:
            break
        b.add_sphere(ctr, rad, c_seg, c_ring, cell, 0, "root")


def build_trunk_felled(b: PropBuilder, lod: int):
    """Felled trunk with its crown lying along +Y (y=0 splintered break -> y=9 m log -> y=9..13.8 m wide crown)."""
    t_seg = (10, 6, 4)[lod]
    b_seg = (6, 4, 3)[lod]
    c_seg = (9, 6, 4)[lod]
    c_ring = (6, 4, 3)[lod]

    if lod == 2:
        b.add_cylinder((0.0, 0.2, 0.36), (0.0, 9.4, 0.28), 0.36, 0.18, 4, "bark_dark", 0, "root")
        b.add_sphere((0.0, 11.2, 1.15), (2.3, 2.4, 1.15), 5, 3, "foliage_mid", 0, "root")
        b.add_sphere((-1.3, 10.6, 0.85), (1.3, 1.4, 0.85), 4, 3, "foliage_dry", 0, "root")
        b.add_sphere((1.3, 11.4, 0.90), (1.3, 1.4, 0.90), 4, 3, "foliage_dark", 0, "root")
        return

    # Jagged splintered break end at y = 0 (exposed pale wood splinters + torn bark collar).
    b.add_cylinder((0.0, 0.0, 0.38), (0.0, 0.55, 0.38), 0.44, 0.40, t_seg, "bark_dark", 0, "root")
    b.add_tapered_box((0.0, -0.18, 0.38), (0.52, 0.04), (0.14, 0.08), 0.45, (90, 0, 0), "wood", 0, "root")
    if lod == 0:
        for sx, sz in ((-0.14, 0.12), (0.16, 0.10), (-0.06, -0.14), (0.10, -0.10)):
            b.add_tapered_box((sx, -0.22, 0.38 + sz), (0.12, 0.36), (0.02, 0.02), 0.14, (78, sx * 40, 0), "wood", 0, "root")

    # Main fallen log along +Y resting cleanly near ground (z ~ 0.32 m)
    b.add_arc_tube(
        [(0.0, 0.45, 0.38), (0.08, 3.2, 0.35), (-0.06, 6.4, 0.31), (0.04, 9.2, 0.28), (0.0, 11.8, 0.62)],
        [0.40, 0.35, 0.28, 0.20, 0.11],
        t_seg,
        "bark_dark",
        0,
        "root",
    )

    # Snapped branch stubs along trunk shaft
    stubs = [
        ((0.05, 3.0, 0.40), (0.75, 3.6, 1.10)),
        ((-0.04, 5.2, 0.34), (-0.82, 5.9, 0.92)),
        ((0.02, 7.2, 0.30), (0.85, 8.1, 1.25)),
    ]
    for p0, p1 in stubs:
        b.add_cylinder(p0, p1, 0.11, 0.05, b_seg, "bark_dark", 0, "root")

    # Spreading shattered crown branches inside foliage mass (y = 8.8..13.5 m, 4.4 m wide)
    crown_limbs = [
        ((0.04, 8.8, 0.30), (-1.9, 10.9, 1.1)),
        ((0.04, 9.0, 0.32), (1.85, 11.1, 1.2)),
        ((0.02, 9.5, 0.42), (-0.6, 13.0, 1.7)),
        ((0.02, 9.6, 0.44), (1.1, 12.8, 1.5)),
    ]
    for p0, p1 in crown_limbs:
        b.add_cylinder(p0, p1, 0.14, 0.06, b_seg, "bark_dark", 0, "root")

    # Broad crushed crown cushions spanning ~4.2 m width (X) and ~4.8 m length (Y)
    leaf_clusters = [
        ((0.0, 10.8, 1.15), (1.75, 1.85, 1.05), "foliage_mid"),
        ((-1.45, 10.7, 0.92), (1.30, 1.40, 0.85), "foliage_dry"),
        ((1.48, 10.9, 0.96), (1.30, 1.45, 0.88), "foliage_dark"),
        ((-0.45, 12.6, 1.38), (1.25, 1.25, 0.92), "foliage_mid"),
        ((0.85, 12.5, 1.22), (1.20, 1.25, 0.84), "foliage_dry"),
        ((-0.95, 9.4, 0.78), (1.10, 1.15, 0.72), "foliage_dark"),
        ((0.95, 9.5, 0.82), (1.10, 1.10, 0.74), "foliage_dry"),
        ((0.05, 11.6, 1.85), (1.10, 1.15, 0.76), "foliage_mid"),
    ]
    for idx, (ctr, rad, cell) in enumerate(leaf_clusters):
        if lod == 1 and idx >= 5:
            break
        b.add_sphere(ctr, rad, c_seg, c_ring, cell, 0, "root")


def build_wreck_siege(b: PropBuilder, lod: int):
    """Smashed siege engine wreck: canted charred frame, snapped throwing arm, buckled & detached wheels, stone ammo."""
    if lod == 2:
        b.add_box((0.0, 0.0, 0.34), (1.85, 2.30, 0.42), (-6, 10, 14), "charred", 0, "root")
        b.add_box((-0.18, 0.10, 0.92), (0.28, 1.95, 0.24), (36, -8, -18), "wood", 0, "root")
        b.add_cylinder((-0.98, 0.55, 0.42), (-1.14, 0.55, 0.42), 0.46, 0.46, 6, "metal", 0, "root")
        b.add_cylinder((1.15, -0.60, 0.14), (1.15, -0.60, 0.32), 0.46, 0.46, 6, "wood", 0, "root")
        return

    simple_wheels = lod >= 1
    # Canted main chassis timbers (half sunken/charred from catastrophic smash).
    b.add_box((-0.58, 0.0, 0.34), (0.24, 2.35, 0.24), (-5, 8, 12), "charred", 0, "root")
    b.add_box((0.55, 0.08, 0.24), (0.24, 2.25, 0.24), (-8, 10, 10), "wood", 0, "root")
    b.add_box((0.0, -0.78, 0.28), (1.42, 0.24, 0.22), (4, -10, 8), "charred", 0, "root")
    b.add_box((0.04, 0.72, 0.36), (1.40, 0.24, 0.22), (6, -8, 14), "wood", 0, "root")
    b.add_box((0.0, 0.0, 0.34), (0.98, 1.20, 0.10), (-6, 9, 12), "charred", 0, "root")

    # Upright A-frame posts snapped partway up
    b.add_box((-0.44, -0.15, 0.76), (0.18, 0.20, 0.95), (14, -16, 12), "charred", 0, "root")
    b.add_box((0.46, 0.02, 0.68), (0.18, 0.20, 0.72), (-18, 14, -8), "wood", 0, "root")

    # Snapped throwing arm & torn torsion rope skein jutting diagonally upward.
    b.add_box((-0.08, 0.18, 0.96), (0.18, 1.90, 0.18), (38, -12, -20), "wood", 0, "root")
    b.add_box((-0.18, 0.88, 1.54), (0.36, 0.42, 0.18), (32, -8, -18), "metal", 0, "root")
    b.add_cylinder((-0.52, 0.0, 0.56), (0.50, 0.06, 0.48), 0.16, 0.16, 7 if lod == 0 else 5, "straw", 0, "root")

    # Intact rear-left wheel + buckled front-left wheel + detached wheel fallen flat to the right.
    b.add_wheel((-0.98, 0.68, 0.46), 0.16, 0.46, 8 if lod == 0 else 6, "metal", "wood", 0, "root", simple=simple_wheels)
    b.add_wheel((-0.88, -0.78, 0.36), 0.15, 0.42, 6, "metal", "charred", 0, "root", simple=simple_wheels)
    # Fallen detached wheel resting flat on turf beside wreck: build spokes via tilted axle
    b.add_cylinder((1.15, -0.45, 0.04), (1.15, -0.45, 0.20), 0.46, 0.46, 8 if lod == 0 else 6, "metal", 0, "root")
    b.add_cylinder((1.15, -0.45, 0.03), (1.15, -0.45, 0.22), 0.34, 0.34, 8 if lod == 0 else 6, "wood", 0, "root")

    # Scattered broken planks + spilled siege stone ammo beside frame.
    b.add_sphere((-0.24, -1.12, 0.22), (0.24, 0.22, 0.20), 6, 4, "stone", 0, "root")
    if lod == 0:
        b.add_sphere((0.44, 1.05, 0.20), (0.21, 0.22, 0.19), 6, 4, "stone", 0, "root")
        b.add_box((0.36, -0.96, 0.12), (0.14, 0.92, 0.08), (4, 8, -32), "wood", 0, "root")
        b.add_box((-0.62, -0.98, 0.14), (0.12, 0.84, 0.08), (-5, 4, 24), "charred", 0, "root")


def build_fire_small(b: PropBuilder, lod: int):
    """~1.5 m cell fire patch: charred earth crater, crossed scorched logs, glowing coal bed, layered orange/yellow flames."""
    seg = (8, 6, 4)[lod]
    if lod == 2:
        b.add_cylinder((0.0, 0.0, 0.0), (0.0, 0.0, 0.08), 0.72, 0.64, 6, "charred", 0, "root")
        b.add_cylinder((0.0, 0.0, 0.08), (0.0, 0.0, 1.18), 0.44, 0.04, 5, "flame_orange", 0, "root")
        b.add_cylinder((0.0, 0.0, 0.12), (0.0, 0.0, 0.92), 0.26, 0.03, 4, "flame_yellow", 0, "root")
        return

    # Scorched ash & glowing ember saucer (1.45 m wide cell footprint)
    b.add_cylinder((0.0, 0.0, 0.0), (0.0, 0.0, 0.07), 0.72, 0.66, 12 if lod == 0 else 8, "charred", 0, "root")
    b.add_cylinder((0.0, 0.0, 0.06), (0.0, 0.0, 0.12), 0.48, 0.42, 10 if lod == 0 else 6, "ember", 0, "root")

    # Crossed burning billets / logs around the center
    logs = [
        ((-0.48, -0.22, 0.09), (0.46, 0.24, 0.16), "charred"),
        ((0.42, -0.34, 0.09), (-0.36, 0.42, 0.15), "bark_dark"),
        ((-0.18, -0.52, 0.10), (0.26, 0.46, 0.14), "charred"),
    ]
    for p0, p1, cell in logs:
        b.add_cylinder(p0, p1, 0.08, 0.065, 6 if lod == 0 else 4, cell, 0, "root")

    # Glowing coal chunks in center
    coal_count = 7 if lod == 0 else 3
    for i in range(coal_count):
        a = 2 * math.pi * i / coal_count
        r = 0.18 + 0.12 * (i % 2)
        b.add_sphere((math.cos(a) * r, math.sin(a) * r, 0.14), (0.11, 0.10, 0.08), 5, 4, "ember", 0, "root")

    # Outer licking orange flame tongues
    outer_tongues = [
        (0.0, 0.0, 0.10, 0.04, 0.02, 1.32, 0.30),
        (0.24, 0.10, 0.10, 0.34, 0.16, 1.04, 0.22),
        (-0.22, 0.16, 0.10, -0.30, 0.24, 0.98, 0.21),
        (0.10, -0.26, 0.10, 0.18, -0.34, 0.92, 0.20),
        (-0.20, -0.18, 0.10, -0.28, -0.26, 0.88, 0.19),
        (0.32, -0.12, 0.09, 0.42, -0.16, 0.72, 0.16),
        (-0.08, 0.30, 0.09, -0.12, 0.38, 0.76, 0.17),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, rad) in enumerate(outer_tongues):
        if lod == 1 and idx >= 4:
            break
        xm = (x0 + x1) * 0.5 + 0.06 * math.sin(idx * 2.1)
        ym = (y0 + y1) * 0.5 - 0.05 * math.cos(idx * 1.7)
        zm = z0 + (z1 - z0) * 0.52
        b.add_arc_tube([(x0, y0, z0), (xm, ym, zm), (x1, y1, z1)], [rad, rad * 0.55, 0.015], seg, "flame_orange", 0, "root")

    # Inner bright yellow hot flame core
    inner_tongues = [
        (0.0, 0.0, 0.12, 0.02, -0.02, 0.98, 0.19),
        (0.14, 0.06, 0.12, 0.20, 0.10, 0.72, 0.13),
        (-0.14, -0.08, 0.12, -0.20, -0.12, 0.68, 0.12),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, rad) in enumerate(inner_tongues):
        if lod == 1 and idx >= 2:
            break
        b.add_arc_tube(
            [(x0, y0, z0), ((x0 + x1) * 0.5 - 0.04, (y0 + y1) * 0.5 + 0.03, (z0 + z1) * 0.5), (x1, y1, z1)],
            [rad, rad * 0.52, 0.012],
            seg,
            "flame_yellow",
            0,
            "root",
        )


def build_hedgerow(b: PropBuilder, lod: int):
    """~1.5 m wide hawthorn & briar hedgerow cell segment: root bank, gnarled woody stems, layered thorny foliage."""
    if lod == 2:
        b.add_box((0.0, 0.0, 0.14), (1.48, 0.72, 0.28), (0, 0, 0), "bark_dark", 0, "root")
        b.add_sphere((-0.44, 0.02, 0.78), (0.48, 0.52, 0.64), 5, 3, "foliage_dark", 0, "root")
        b.add_sphere((0.0, -0.03, 0.88), (0.54, 0.56, 0.68), 5, 3, "foliage_mid", 0, "root")
        b.add_sphere((0.44, 0.03, 0.76), (0.48, 0.50, 0.62), 5, 3, "foliage_dark", 0, "root")
        return

    # Earthy & tangled root bank base anchored at z = 0.
    b.add_tapered_box((0.0, 0.0, 0.14), (1.50, 0.92), (1.38, 0.72), 0.28, (0, 0, 0), "bark_dark", 0, "root")

    # Gnarled twisting hawthorn stems & protruding briar branches.
    stem_seg = 6 if lod == 0 else 4
    stems = [
        ((-0.52, -0.06, 0.08), (-0.46, 0.04, 0.95), 0.08, 0.04),
        ((-0.18, 0.10, 0.08), (-0.22, -0.08, 1.08), 0.09, 0.04),
        ((0.16, -0.10, 0.08), (0.20, 0.06, 1.04), 0.085, 0.04),
        ((0.50, 0.04, 0.08), (0.44, -0.06, 0.92), 0.075, 0.035),
    ]
    for idx, (p0, p1, r0, r1) in enumerate(stems):
        b.add_cylinder(p0, p1, r0, r1, stem_seg, "bark_dark", 0, "root")
        if lod == 0:
            side_y = 0.48 if idx % 2 == 0 else -0.48
            b.add_cylinder(
                ((p0[0] + p1[0]) * 0.5, 0.0, 0.62 + 0.08 * idx),
                (p1[0] * 1.05, side_y, 0.96 + 0.06 * (idx % 2)),
                0.045,
                0.018,
                5,
                "wood",
                0,
                "root",
            )

    # Dense overlapping foliage clusters spanning 1.5 m along X with irregular natural silhouette.
    f_seg, f_ring = (8, 5) if lod == 0 else (6, 4)
    main_clusters = [
        ((-0.52, 0.06, 0.74), (0.44, 0.48, 0.52), "foliage_dark"),
        ((-0.24, -0.10, 0.88), (0.46, 0.50, 0.56), "foliage_mid"),
        ((0.02, 0.08, 0.96), (0.50, 0.52, 0.58), "foliage_dark"),
        ((0.28, -0.06, 0.84), (0.44, 0.48, 0.52), "foliage_mid"),
        ((0.54, 0.04, 0.72), (0.42, 0.46, 0.48), "foliage_dark"),
        ((-0.36, 0.24, 0.56), (0.34, 0.34, 0.36), "foliage_mid"),
        ((0.32, -0.26, 0.54), (0.34, 0.34, 0.36), "foliage_dry"),
    ]
    for ctr, rad, cell in main_clusters:
        b.add_sphere(ctr, rad, f_seg, f_ring, cell, 0, "root")

    if lod == 1:
        accents = [
            ((-0.40, -0.22, 1.02), (0.28, 0.28, 0.28), "foliage_dry"),
            ((-0.08, 0.22, 1.18), (0.30, 0.28, 0.30), "foliage_mid"),
            ((0.24, 0.18, 1.12), (0.28, 0.26, 0.26), "foliage_dry"),
            ((0.48, -0.20, 0.88), (0.26, 0.28, 0.26), "foliage_mid"),
        ]
        for ctr, rad, cell in accents:
            b.add_sphere(ctr, rad, 5, 3, cell, 0, "root")
    else:
        accents = [
            ((-0.58, -0.18, 0.52), (0.28, 0.28, 0.28), "foliage_dark"),
            ((-0.44, 0.18, 1.08), (0.30, 0.28, 0.28), "foliage_dry"),
            ((-0.18, -0.26, 1.12), (0.28, 0.26, 0.28), "foliage_mid"),
            ((0.06, -0.18, 1.24), (0.32, 0.28, 0.26), "foliage_dry"),
            ((0.36, 0.22, 1.06), (0.28, 0.26, 0.28), "foliage_mid"),
            ((0.58, -0.16, 0.50), (0.26, 0.28, 0.26), "foliage_dark"),
            ((-0.02, 0.30, 0.62), (0.30, 0.26, 0.30), "foliage_mid"),
            ((0.14, -0.32, 0.46), (0.28, 0.24, 0.28), "foliage_dark"),
            ((-0.34, -0.30, 0.44), (0.26, 0.24, 0.26), "foliage_dry"),
        ]
        for ctr, rad, cell in accents:
            b.add_sphere(ctr, rad, 6, 4, cell, 0, "root")


def build_wall_drystone(b: PropBuilder, lod: int):
    """~1.5 m battered coursed fieldstone drystone wall section with rubble hearting and upright coping stones."""
    if lod == 2:
        b.add_tapered_box((0.0, 0.0, 0.48), (1.50, 0.84), (1.46, 0.56), 0.96, (0, 0, 0), "stone", 0, "root")
        for i in range(4):
            cx = -0.54 + i * 0.36
            b.add_box((cx, 0.0, 1.06), (0.30, 0.44, 0.22), (0, 3 if i % 2 == 0 else -4, 0), "charred", 0, "root")
        return

    # Inner battered rubble mortarless core grounded cleanly at z = 0.
    b.add_tapered_box((0.0, 0.0, 0.48), (1.50, 0.82), (1.46, 0.52), 0.96, (0, 0, 0), "charred", 0, "root")

    # Coursed outer fieldstones on both faces (+Y and -Y) tapering inward with height.
    stone_palette = ("stone", "cloth_cream", "stone", "charred")
    courses = (
        (0.16, 0.30, 0.34, 4 if lod == 1 else 5),
        (0.46, 0.26, 0.30, 4 if lod == 1 else 6),
        (0.76, 0.22, 0.26, 4 if lod == 1 else 5),
    )
    for c_idx, (cz, ch, dy, count) in enumerate(courses):
        span = 1.46
        step = span / count
        y_off = 0.28 - c_idx * 0.055
        for side_y in (-1.0, 1.0):
            for i in range(count):
                jx = (0.04 if (i + c_idx) % 2 == 0 else -0.03)
                cx = -span * 0.5 + (i + 0.5) * step + jx * 0.4
                sx = step * (0.94 if (i + c_idx) % 3 else 0.88)
                sz = ch * (0.96 if i % 2 == 0 else 0.88)
                yaw = (4.0 if (i + int(side_y)) % 2 == 0 else -3.5)
                pitch = (2.5 if c_idx == 1 else -2.0) * side_y
                cell = stone_palette[(i + c_idx * 2 + (0 if side_y > 0 else 1)) % len(stone_palette)]
                b.add_box((cx, side_y * y_off, cz), (sx, dy, sz), (pitch, 0, yaw), cell, 0, "root")

    # Additional small chinking stones on LOD0 to hit authentic drystone texture.
    if lod == 0:
        for i in range(12):
            cx = -0.66 + (i * 0.12)
            sy = 0.32 if i % 2 == 0 else -0.32
            cz = 0.31 + (i % 3) * 0.28
            b.add_box((cx, sy * 0.92, cz), (0.14, 0.16, 0.09), (0, 5 if i % 2 else -5, 12), "charred", 0, "root")

    # Upright angled coping stones crowning the wall crest (z = 0.94..1.20 m).
    coping_count = 7 if lod == 0 else 5
    step = 1.46 / coping_count
    for i in range(coping_count):
        cx = -0.73 + (i + 0.5) * step
        sh = 0.24 if i % 2 == 0 else 0.20
        cell = "stone" if i % 3 != 1 else "charred"
        b.add_tapered_box(
            (cx, 0.0, 0.95 + sh * 0.5),
            (step * 0.92, 0.46),
            (step * 0.78, 0.26),
            sh,
            (4 if i % 2 == 0 else -3, 3, 2 if i % 2 == 0 else -2),
            cell,
            0,
            "root",
        )


def build_bridge_wood(b: PropBuilder, lod: int):
    """~3.0 m wide x 4.5 m long heavy timber cart bridge section: driven piles, stringers, deck planks, wheel kerbs & braced rails."""
    if lod == 2:
        # Four heavy piles + wide timber deck + two top handrails (84 tris)
        for px in (-1.25, 1.25):
            for py in (-1.45, 1.45):
                b.add_box((px, py, 0.28), (0.28, 0.28, 0.56), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((0.0, 0.0, 0.56), (2.96, 4.40, 0.24), (0, 0, 0), "wood", 0, "root")
        b.add_box((-1.36, 0.0, 1.06), (0.16, 4.40, 0.36), (0, 0, 0), "wood", 0, "root")
        b.add_box((1.36, 0.0, 1.06), (0.16, 4.40, 0.36), (0, 0, 0), "wood", 0, "root")
        return

    pile_seg = 6 if lod == 0 else 5
    # Driven support pile bents anchored at z = 0 supporting crosshead caps (z = 0.42 m).
    for py in (-1.65, 1.65):
        for px in (-1.20, 0.0, 1.20):
            b.add_cylinder((px, py, 0.0), (px, py, 0.46), 0.16, 0.14, pile_seg, "bark_dark", 0, "root")
        b.add_box((0.0, py, 0.42), (2.85, 0.24, 0.20), (0, 0, 0), "bark_dark", 0, "root")

    # Three longitudinal heavy stringer beams spanning Y = -2.22..+2.22 m.
    for sx in (-1.05, 0.0, 1.05):
        b.add_box((sx, 0.0, 0.55), (0.22, 4.46, 0.20), (0, 0, 0), "bark_dark", 0, "root")

    # Crosswise oak carriageway deck planks with subtle plank camber & joints.
    plank_count = 13 if lod == 0 else 6
    plank_span = 4.42
    pitch_y = plank_span / plank_count
    for i in range(plank_count):
        py = -plank_span * 0.5 + (i + 0.5) * pitch_y
        arch_z = 0.03 * (1.0 - (py / 2.25) ** 2)
        cell = "wood" if i % 4 != 2 else "bark_dark"
        b.add_box((0.0, py, 0.70 + arch_z), (2.94, pitch_y * 0.90, 0.11), (0, 0, 0), cell, 0, "root")

    # Wheel-guard kerb timbers & parapet handrail assemblies along both X edges.
    for sx in (-1.34, 1.34):
        b.add_box((sx * 0.94, 0.0, 0.80), (0.14, 4.40, 0.14), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((sx, 0.0, 1.24), (0.12, 4.44, 0.10), (0, 0, 0), "wood", 0, "root")
        b.add_box((sx, 0.0, 1.00), (0.08, 4.38, 0.07), (0, 0, 0), "wood", 0, "root")
        post_ys = (-1.85, -0.62, 0.62, 1.85) if lod == 0 else (-1.65, 0.0, 1.65)
        for py in post_ys:
            b.add_box((sx, py, 0.98), (0.14, 0.14, 0.62), (0, 0, 0), "bark_dark", 0, "root")
            if lod == 0:
                b.add_box((sx, py, 0.74), (0.18, 0.18, 0.08), (0, 0, 0), "metal", 0, "root")
        if lod == 0:
            # Diagonal knee braces & iron strap gussets on outside of posts
            for py in (-1.85, 0.62):
                b.add_cylinder((sx, py, 0.80), (sx, py + 0.72, 1.20), 0.045, 0.045, 4, "wood", 0, "root")
            for py in (-1.65, 1.65):
                b.add_box((sx, py, 0.56), (0.18, 0.26, 0.18), (0, 0, 0), "metal", 0, "root")


def build_bridge_foot(b: PropBuilder, lod: int):
    """~1.8 m wide x 3.0 m long lightweight plank footbridge: slender trestle piers, twin poles, slatted deck, single rope rail."""
    if lod == 2:
        b.add_box((-0.65, 0.0, 0.18), (0.16, 1.60, 0.36), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((0.65, 0.0, 0.18), (0.16, 1.60, 0.36), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((0.0, 0.0, 0.42), (1.76, 2.94, 0.14), (0, 0, 0), "wood", 0, "root")
        b.add_box((-0.78, -1.10, 0.78), (0.10, 0.10, 0.68), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((-0.78, 1.10, 0.78), (0.10, 0.10, 0.68), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((-0.78, 0.0, 1.04), (0.08, 2.50, 0.08), (0, 0, 0), "straw", 0, "root")
        return

    leg_seg = 5 if lod == 0 else 4
    # Two lightweight trestle frames grounded at z = 0.
    for py in (-1.05, 1.05):
        b.add_cylinder((-0.64, py, 0.0), (-0.54, py, 0.42), 0.075, 0.065, leg_seg, "bark_dark", 0, "root")
        b.add_cylinder((0.64, py, 0.0), (0.54, py, 0.42), 0.075, 0.065, leg_seg, "bark_dark", 0, "root")
        b.add_cylinder((-0.58, py, 0.14), (0.58, py, 0.36), 0.045, 0.045, 4, "bark_dark", 0, "root")
        b.add_box((0.0, py, 0.39), (1.42, 0.12, 0.10), (0, 0, 0), "bark_dark", 0, "root")

    # Twin longitudinal pole stringers.
    for sx in (-0.48, 0.48):
        b.add_cylinder((sx, -1.48, 0.46), (sx, 1.48, 0.46), 0.065, 0.065, leg_seg, "wood", 0, "root")

    # Narrow slatted wooden walkway planks across twin stringers.
    slat_count = 11 if lod == 0 else 7
    slat_span = 2.90
    pitch_y = slat_span / slat_count
    for i in range(slat_count):
        py = -slat_span * 0.5 + (i + 0.5) * pitch_y
        b.add_box((0.0, py, 0.54), (1.72, pitch_y * 0.80, 0.07), (0, 0, 1.5 if i % 2 else -1.5), "wood", 0, "root")

    # Single-sided upright posts with sagged hemp rope handrail along X = -0.76 m.
    post_ys = (-1.28, -0.42, 0.42, 1.28) if lod == 0 else (-1.20, 0.0, 1.20)
    for py in post_ys:
        b.add_cylinder((-0.76, py, 0.42), (-0.78, py, 1.08), 0.055, 0.048, leg_seg, "bark_dark", 0, "root")
        b.add_cylinder((-0.78, py, 0.96), (-0.78, py, 1.03), 0.072, 0.072, 4, "straw", 0, "root")
        b.add_cylinder((-0.76, py, 0.50), (-0.76, py, 0.57), 0.072, 0.072, 4, "straw", 0, "root")

    rope_pts = []
    rope_steps = 8 if lod == 0 else 4
    for i in range(rope_steps + 1):
        u = -1.28 + 2.56 * (i / rope_steps)
        seg_u = ((u + 1.28) % 0.853) / 0.853
        sag = 0.06 * math.sin(seg_u * math.pi)
        rope_pts.append((-0.78, u, 1.00 - sag))
    b.add_arc_tube(rope_pts, 0.028, 5 if lod == 0 else 4, "straw", 0, "root")

    if lod == 0:
        # Timber side cleats fastening slats to stringers
        for sx in (-0.66, 0.66):
            b.add_box((sx, 0.0, 0.59), (0.08, 2.86, 0.05), (0, 0, 0), "bark_dark", 0, "root")


def build_boulder(b: PropBuilder, lod: int):
    """~3.0 m x 3.0 m x 2.0 m craggy limestone boulder mass: flat-bottomed main crag, fractured rock slabs, mossy caps."""
    if lod == 2:
        b.add_sphere((0.0, 0.0, 0.72), (1.38, 1.28, 0.92), 6, 4, "stone", 0, "root")
        b.add_sphere((-0.68, 0.52, 0.54), (0.82, 0.76, 0.62), 5, 3, "charred", 0, "root")
        b.add_sphere((0.24, -0.18, 1.42), (0.76, 0.68, 0.28), 5, 3, "foliage_dark", 0, "root")
        return

    # Flat-bottomed base plinth & faceted crag slabs grounded cleanly at z = 0.
    b.add_tapered_box((0.0, 0.0, 0.36), (2.72, 2.48), (2.15, 1.98), 0.72, (0, 0, 14), "charred", 0, "root")

    s_seg, s_ring = (8, 5) if lod == 0 else (6, 4)
    # Primary fractured stone core and interlocking rock shelves (kept above z = 0).
    lobes = [
        ((0.0, 0.0, 0.88), (1.28, 1.18, 0.92), "stone"),
        ((-0.72, -0.44, 0.68), (0.86, 0.82, 0.68), "stone"),
        ((0.78, 0.38, 0.64), (0.82, 0.78, 0.64), "charred"),
        ((-0.42, 0.74, 0.58), (0.76, 0.72, 0.58), "stone"),
        ((0.52, -0.68, 0.52), (0.72, 0.68, 0.52), "cloth_cream"),
        ((-0.10, 0.12, 1.38), (0.88, 0.78, 0.54), "stone"),
    ]
    for idx, (ctr, rad, cell) in enumerate(lobes):
        if lod == 1 and idx == 4:
            continue
        b.add_sphere(ctr, rad, s_seg, s_ring, cell, 0, "root")

    # Angular cleft & strata blocks jutting out for crisp low-poly crag planes.
    strata = [
        ((-0.62, 0.18, 0.96), (0.92, 0.84, 0.62), (18, -22, 32), "stone"),
        ((0.46, -0.22, 1.04), (0.88, 0.74, 0.58), (-14, 24, -18), "charred"),
        ((0.08, 0.56, 0.86), (0.96, 0.68, 0.54), (24, 12, 8), "stone"),
        ((-0.24, -0.64, 0.74), (0.84, 0.64, 0.48), (-22, -16, 42), "stone"),
    ]
    for ctr, sz, rot, cell in strata:
        b.add_box(ctr, sz, rot, cell, 0, "root")

    if lod == 0:
        # Loose scree boulders nestled around the foot at z = 0..0.38 m
        scree = [
            ((-1.18, -0.78, 0.20), (0.48, 0.42, 0.32), (8, 12, 24), "stone"),
            ((1.16, 0.62, 0.18), (0.44, 0.46, 0.28), (-6, 14, -30), "charred"),
            ((0.92, -0.92, 0.16), (0.42, 0.36, 0.26), (12, -8, 15), "stone"),
            ((-0.96, 0.94, 0.19), (0.46, 0.38, 0.30), (-10, 6, 40), "cloth_cream"),
        ]
        for ctr, sz, rot, cell in scree:
            b.add_box(ctr, sz, rot, cell, 0, "root")

    # Moss & lichen cap cushions on sheltered north/upper ledges.
    moss_caps = [
        ((-0.18, 0.22, 1.82), (0.58, 0.48, 0.18), "foliage_dark"),
        ((0.36, 0.38, 1.38), (0.48, 0.42, 0.16), "foliage_mid"),
        ((-0.78, 0.56, 1.02), (0.42, 0.38, 0.15), "foliage_dark"),
    ]
    for idx, (ctr, rad, cell) in enumerate(moss_caps):
        if lod == 1 and idx >= 2:
            continue
        b.add_sphere(ctr, rad, 6 if lod == 0 else 5, 3, cell, 0, "root")


def build_pen_herd(b: PropBuilder, lod: int):
    """~1.5 m x 1.5 m rustic split-rail livestock herd pen corner & swung gate bay with trough and hay."""
    if lod == 2:
        for px, py in ((-0.65, -0.65), (0.65, -0.65), (-0.65, 0.65), (0.65, 0.65)):
            b.add_box((px, py, 0.64), (0.16, 0.16, 1.28), (0, 0, 0), "bark_dark", 0, "root")
        b.add_box((0.0, -0.65, 0.96), (1.38, 0.10, 0.14), (0, 0, 0), "wood", 0, "root")
        b.add_box((0.0, -0.65, 0.50), (1.38, 0.10, 0.14), (0, 0, 0), "wood", 0, "root")
        b.add_box((-0.65, 0.0, 0.74), (0.10, 1.38, 0.48), (0, 0, 0), "wood", 0, "root")
        return

    post_seg = 6 if lod == 0 else 5
    # Heavy oak fence posts driven vertically at z = 0..1.32 m around corner + gate opening.
    posts = [
        ((-0.66, -0.66), 1.32),
        ((0.02, -0.66), 1.26),
        ((0.66, -0.66), 1.30),
        ((-0.66, 0.02), 1.24),
        ((-0.66, 0.66), 1.32),
        ((0.66, 0.66), 1.28),
    ]
    for idx, ((px, py), ph) in enumerate(posts):
        if lod == 1 and idx in (1, 3):
            continue
        b.add_cylinder((px, py, 0.0), (px, py, ph), 0.095, 0.08, post_seg, "bark_dark", 0, "root")
        # Rope lashing collars at top & middle rail heights
        b.add_cylinder((px, py, 0.96), (px, py, 1.05), 0.105, 0.105, 5, "straw", 0, "root")
        if lod == 0:
            b.add_cylinder((px, py, 0.56), (px, py, 0.64), 0.105, 0.105, 5, "straw", 0, "root")

    # Two fixed split-rail fence sides along back (-Y) and left (-X).
    rail_heights = (0.36, 0.68, 1.02)
    for rz in rail_heights:
        b.add_box((0.0, -0.66, rz), (1.38, 0.08, 0.11), (0, 0, 0), "wood", 0, "root")
        b.add_box((-0.66, 0.0, rz), (0.08, 1.38, 0.11), (0, 0, 0), "wood", 0, "root")

    # Hinged hurdle gate along front/right edge (+Y, slightly ajar inward at 18 deg) with diagonal Z-brace.
    for rz in (0.38, 0.72, 1.02):
        b.add_box((0.04, 0.56, rz), (1.28, 0.07, 0.10), (0, 0, -14), "wood", 0, "root")
    b.add_cylinder((-0.56, 0.68, 0.34), (0.58, 0.42, 1.04), 0.045, 0.045, 4, "bark_dark", 0, "root")
    if lod == 0:
        b.add_box((-0.54, 0.66, 0.98), (0.14, 0.10, 0.08), (0, 0, -14), "metal", 0, "root")
        b.add_box((-0.54, 0.66, 0.42), (0.14, 0.10, 0.08), (0, 0, -14), "metal", 0, "root")

    # Wooden feed/water trough and golden hay bundle inside the pen corner.
    b.add_box((-0.14, -0.22, 0.16), (0.64, 0.32, 0.16), (0, 0, 8), "wood", 0, "root")
    b.add_box((-0.14, -0.22, 0.25), (0.56, 0.24, 0.08), (0, 0, 8), "straw", 0, "root")
    b.add_sphere((0.34, -0.18, 0.18), (0.28, 0.24, 0.20), 6 if lod == 0 else 5, 4 if lod == 0 else 3, "straw", 0, "root")


UNIT_SPECS = [
    ("crew", "U_crew_a", make_units.HUMANOID_BONES, "RIG_humanoid", make_units.make_humanoid_anims, build_crew,
     "Siege and pontoon crewman with leather matross apron, coiled rope baldric, padded engineer cap and heavy handspike."),
    ("cattle", "U_cattle_a", CATTLE_BONES, "RIG_quadruped", make_cattle_anims, build_cattle,
     "Horned draught/range cattle with russet-brindle hide, dewlap, swept ivory horns (1.54 m), halter rope and 6 quadruped animations."),
    ("drovers", "U_drovers_a", make_units.HUMANOID_BONES, "RIG_humanoid", make_units.make_humanoid_anims, build_drovers,
     "Drovers specialist unit with wide-brimmed slouch hat, team tabard & capelet, goad crook staff, whip coil and cattle horn."),
]

PROP_SPECS = [
    ("P_tree_grove", "P_tree", build_tree_grove, "14 m dry-grove oak tree with buttressed roots and multi-lobe dry/olive foliage canopy."),
    ("P_trunk_felled", "P_trunk", build_trunk_felled, "Felled grove trunk along +Y with jagged splintered butt and wide crushed leafy crown."),
    ("P_wreck_siege", "P_wreck", build_wreck_siege, "Smashed siege engine wreck with canted charred frame, snapped arm, buckled/detached wheels and stone ammo."),
    ("P_fire_small", "P_fire", build_fire_small, "Small 1.5 m burning ground patch with charred logs, glowing embers and layered orange/yellow flames."),
    ("P_hedgerow_a", "P_hedgerow", build_hedgerow, "Dense hawthorn/briar hedgerow barrier segment with gnarled woody stems and layered foliage clusters."),
    ("P_wall_drystone", "P_drystone", build_wall_drystone, "Battered coursed fieldstone drystone wall segment with rubble core and upright coping stones."),
    ("P_bridge_wood", "P_bridgewood", build_bridge_wood, "Heavy timber cart bridge span with pile piers, longitudinal stringers, deck planks, wheel kerbs and braced handrails."),
    ("P_bridge_foot", "P_bridgefoot", build_bridge_foot, "Narrow lightweight plank footbridge with trestle legs, slatted walkway and rope-lashed single handrail."),
    ("P_boulder_a", "P_boulder", build_boulder, "Craggy flat-bottomed limestone/granite boulder mass with fractured stone facets, flanking slabs and mossy cap patches."),
    ("P_pen_herd", "P_herdpen", build_pen_herd, "Rustic split-rail livestock herd pen corner and hinged gate section with oak posts, diagonal brace and rope lashings."),
]


def save_asset_blend_and_meta(asset_name, meta_dict):
    blend_path = os.path.join(blender_cli.REPO_ROOT, "art", "source", f"{asset_name}.blend")
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, compress=True)
    blender_cli.write_json(
        os.path.join(blender_cli.REPO_ROOT, "art", "source", f"{asset_name}.source.json"),
        meta_dict,
    )
    return blend_path


def generate_unit(unit_id, asset_name, bones, rig_name, anim_fn, builder_fn, summary, palette, palette_png_path):
    scene = make_units.reset_scene()
    rig = make_units.make_rig(scene, rig_name, bones)
    material = make_units.make_palette_material(palette_png_path)
    bone_names = [b[0] for b in bones]

    counts = {}
    for lod in (0, 1, 2):
        col = bpy.data.collections.new(f"LOD{lod}")
        scene.collection.children.link(col)
        builder = make_units.UnitBuilder(palette, bone_names)
        builder_fn(builder, lod)
        min_t, max_t = LOD_TARGETS[lod]
        _, tris = builder.finalize(f"{asset_name}_LOD{lod}", rig, col, material, min_t, max_t)
        counts[f"LOD{lod}"] = tris

    anim_fn(rig, scene)
    blend_path = save_asset_blend_and_meta(
        asset_name,
        {
            "name": asset_name,
            "unitType": unit_id,
            "source": "script",
            "id": asset_name,
            "url": "tools/blender/make_m5_assets.py",
            "author": "SmiteNot procedural generator",
            "licence": "CC0",
            "credit": "Generated by tools/blender/make_m5_assets.py",
            "changes": summary,
            "created": "2026-11-15",
        },
    )
    print(f"make_m5_assets: {asset_name} ({unit_id}) -> LOD0={counts['LOD0']} LOD1={counts['LOD1']} LOD2={counts['LOD2']} -> {blend_path}")


def generate_prop(asset_name, mesh_prefix, builder_fn, summary, palette, palette_png_path):
    scene = make_units.reset_scene()
    material = make_units.make_palette_material(palette_png_path)

    counts = {}
    for lod in (0, 1, 2):
        col = bpy.data.collections.new(f"LOD{lod}")
        scene.collection.children.link(col)
        builder = PropBuilder(palette)
        builder_fn(builder, lod)
        min_t, max_t = LOD_TARGETS[lod]
        _, tris = builder.finalize_prop(f"{mesh_prefix}_lod{lod}", col, material, min_t, max_t)
        counts[f"LOD{lod}"] = tris

    blend_path = save_asset_blend_and_meta(
        asset_name,
        {
            "name": asset_name,
            "source": "script",
            "id": asset_name,
            "url": "tools/blender/make_m5_assets.py",
            "author": "SmiteNot procedural generator",
            "licence": "CC0",
            "credit": "Generated by tools/blender/make_m5_assets.py",
            "changes": summary,
            "created": "2026-11-15",
        },
    )
    print(f"make_m5_assets: {asset_name} -> LOD0={counts['LOD0']} LOD1={counts['LOD1']} LOD2={counts['LOD2']} -> {blend_path}")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--only", nargs="*", help="optional list of asset stems (e.g. U_crew_a P_tree_grove)")
    args = blender_cli.parse_args(parser)

    palette = blender_cli.load_json("art/palettes/palette.json")
    palette_png_path = make_units.write_palette_png(palette)
    print(f"make_m5_assets: wrote {palette_png_path}")

    only = set(args.only) if args.only else None
    for unit_id, asset_name, bones, rig_name, anim_fn, builder_fn, summary in UNIT_SPECS:
        if only and asset_name not in only and unit_id not in only:
            continue
        generate_unit(unit_id, asset_name, bones, rig_name, anim_fn, builder_fn, summary, palette, palette_png_path)

    for asset_name, mesh_prefix, builder_fn, summary in PROP_SPECS:
        if only and asset_name not in only:
            continue
        generate_prop(asset_name, mesh_prefix, builder_fn, summary, palette, palette_png_path)


if __name__ == "__main__":
    main()
