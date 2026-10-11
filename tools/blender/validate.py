"""Validate a unit or prop .blend against the art rules (tools/blender/art_rules.json) and write a JSON report.

  blender -b art/source/U_testsoldier_a.blend --python tools/blender/validate.py -- --report TestResults/art/U_testsoldier_a.json

Checks triangle budgets per LOD, material count, UVs inside one palette cell per face, applied transforms, names,
bone counts, the team-colour mask, and clip names and lengths. Exits 1 when there are errors (warnings don't fail).
"""
import argparse
import os
import re
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_cli  # noqa: E402

EPSILON = 1e-4


class Report:
    def __init__(self):
        self.errors = []
        self.warnings = []
        self.stats = {}

    def error(self, message):
        self.errors.append(message)

    def warn(self, message):
        self.warnings.append(message)


def triangle_count(mesh_object):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh_object.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        return sum(len(polygon.vertices) - 2 for polygon in mesh.polygons)
    finally:
        evaluated.to_mesh_clear()


def check_transforms(obj, report):
    if any(abs(v) > EPSILON for v in obj.location):
        report.error(f"{obj.name}: location {tuple(round(v, 4) for v in obj.location)} is not at the origin")
    if any(abs(v) > EPSILON for v in obj.rotation_euler) or obj.rotation_mode not in {"XYZ", "QUATERNION"}:
        report.error(f"{obj.name}: rotation is not applied")
    if obj.rotation_mode == "QUATERNION" and any(abs(a - b) > EPSILON for a, b in zip(obj.rotation_quaternion, (1, 0, 0, 0))):
        report.error(f"{obj.name}: rotation is not applied")
    if any(abs(v - 1.0) > EPSILON for v in obj.scale):
        report.error(f"{obj.name}: scale {tuple(round(v, 4) for v in obj.scale)} is not applied")


def cell_span(low, high, grid):
    """First and last palette cell a UV range touches; edges that sit exactly on a cell border don't count."""
    if high - low < 2 * EPSILON:
        cell = min(int((low + high) / 2 * grid), grid - 1)
        return cell, cell
    return min(int((low + EPSILON) * grid), grid - 1), min(int((high - EPSILON) * grid), grid - 1)


def check_uvs(obj, palette, report):
    mesh = obj.data
    if not mesh.uv_layers:
        report.error(f"{obj.name}: no UV map")
        return
    if len(mesh.uv_layers) > 1:
        report.warn(f"{obj.name}: {len(mesh.uv_layers)} UV maps; only the active one is used")
    uv = mesh.uv_layers.active.data
    grid = palette["grid"]
    bad_faces = 0
    for polygon in mesh.polygons:
        us = [uv[i].uv[0] for i in polygon.loop_indices]
        vs = [uv[i].uv[1] for i in polygon.loop_indices]
        if min(us + vs) < -EPSILON or max(us + vs) > 1 + EPSILON:
            bad_faces += 1
            continue
        u_cells = cell_span(min(us), max(us), grid)
        v_cells = cell_span(min(vs), max(vs), grid)
        if u_cells[0] != u_cells[1] or v_cells[0] != v_cells[1]:
            bad_faces += 1
    if bad_faces:
        report.error(f"{obj.name}: {bad_faces} faces have UVs outside a single palette cell ({grid}x{grid} grid)")


def check_team_mask(obj, attribute, report):
    names = [a.name for a in obj.data.color_attributes]
    if attribute not in names:
        report.error(f"{obj.name}: missing the '{attribute}' colour attribute (team-colour mask)")


def check_mesh(obj, lod, rules, palette, report, stats):
    names = rules["names"]
    if not (re.match(names["unit_mesh"], obj.name) or re.match(names["prop_mesh"], obj.name)):
        report.error(f"{obj.name}: name doesn't match U_<type>_<variant>_LOD<n> or P_<prop>_<state>")
    elif obj.name.startswith("U_") and not obj.name.endswith("_" + lod):
        report.error(f"{obj.name}: is in collection {lod} but named for another LOD")

    materials = [slot.material for slot in obj.material_slots if slot.material]
    if len(materials) > rules["max_materials_per_mesh"]:
        report.error(f"{obj.name}: {len(materials)} materials; the limit is {rules['max_materials_per_mesh']}")
    if not materials:
        report.error(f"{obj.name}: no material (assign the shared palette material)")

    check_transforms(obj, report)
    check_uvs(obj, palette, report)
    if obj.name.startswith("U_"):
        check_team_mask(obj, rules["team_mask_attribute"], report)

    triangles = triangle_count(obj)
    stats["triangles"] = stats.get("triangles", 0) + triangles
    stats.setdefault("meshes", []).append({"name": obj.name, "triangles": triangles})


def check_rig(rig, rules, report):
    match = re.match(rules["names"]["rig"], rig.name)
    if not match:
        report.error(f"{rig.name}: rig name doesn't match RIG_<humanoid|quadruped|siege>")
        return
    kind = match.group(1)
    bones = len(rig.data.bones)
    limit = rules["rigs"][kind]["max_bones"]
    if bones > limit:
        report.error(f"{rig.name}: {bones} bones; the {kind} limit is {limit}")
    check_transforms(rig, report)
    report.stats.setdefault("rigs", []).append({"name": rig.name, "kind": kind, "bones": bones})


def check_clips(rules, report):
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    limits = rules["clip_seconds"]
    clips = []
    for action in bpy.data.actions:
        if not re.match(rules["names"]["clip"], action.name):
            report.error(f"{action.name}: clip name doesn't match A_<rig>_<clip>")
        start, end = action.frame_range
        seconds = (end - start) / fps
        if not limits["min"] <= seconds <= limits["max"]:
            report.error(f"{action.name}: {seconds:.2f} s long; clips must be {limits['min']}-{limits['max']} s")
        if not action.use_fake_user and action.users == 0:
            report.warn(f"{action.name}: has no users and no fake user, so Blender won't save it")
        clips.append({"name": action.name, "frames": [start, end], "seconds": round(seconds, 3)})
    report.stats["clips"] = clips
    report.stats["fps"] = fps


def validate(rules, palette):
    report = Report()
    lod_re = re.compile(rules["names"]["lod_collection"])
    lods = sorted((c for c in bpy.data.collections if lod_re.match(c.name)), key=lambda c: c.name)
    if not lods:
        report.error("no LOD collections (LOD0, LOD1, ...)")

    in_lods = set()
    report.stats["lods"] = {}
    for collection in lods:
        stats = {}
        meshes = [o for o in collection.all_objects if o.type == "MESH"]
        if not meshes:
            report.error(f"{collection.name}: no meshes")
        for obj in meshes:
            in_lods.add(obj.name)
            check_mesh(obj, collection.name, rules, palette, report, stats)
        budget = rules["triangle_budgets"].get(collection.name)
        triangles = stats.get("triangles", 0)
        if budget:
            if triangles > budget["max"]:
                report.error(f"{collection.name}: {triangles} triangles; the budget is {budget['max']} ({budget['role']})")
            elif triangles < budget.get("min", 0):
                report.warn(f"{collection.name}: {triangles} triangles, under the {budget['min']}-{budget['max']} target")
        report.stats["lods"][collection.name] = stats

    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.name not in in_lods:
            report.warn(f"{obj.name}: mesh outside the LOD collections won't be exported")
        if obj.type == "ARMATURE":
            check_rig(obj, rules, report)

    check_clips(rules, report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--report", required=True, help="where to write the JSON report")
    args = blender_cli.parse_args(parser)

    rules = blender_cli.load_json("tools/blender/art_rules.json")
    palette = blender_cli.load_json(rules["palette"])
    report = validate(rules, palette)

    result = {
        "file": os.path.relpath(bpy.data.filepath, blender_cli.REPO_ROOT).replace("\\", "/"),
        "blender": bpy.app.version_string,
        "ok": not report.errors,
        "errors": report.errors,
        "warnings": report.warnings,
        "stats": report.stats,
    }
    blender_cli.write_json(args.report, result)
    for line in report.errors:
        print(f"validate: ERROR {line}")
    for line in report.warnings:
        print(f"validate: warning {line}")
    print(f"validate: {len(report.errors)} errors, {len(report.warnings)} warnings -> {args.report}")
    sys.exit(1 if report.errors else 0)


if __name__ == "__main__":
    main()
