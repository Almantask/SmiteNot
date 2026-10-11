"""Export a unit or prop .blend to FBX for Unity: one file per LOD collection plus one with the animation clips.

  blender -b art/source/U_testsoldier_a.blend --python tools/blender/export.py -- --out unity/Assets/_Game/Art/Generated/U_testsoldier_a

Writes <asset>_LOD<n>.fbx for each LOD collection, <asset>_Anims.fbx (rig, LOD0 mesh and every action) when the
file has a rig with actions, and export.json listing what was written. Axes: Blender is Z-up; the exporter writes
Unity's Y-up convention and the Unity AssetPostprocessor bakes the axis conversion, so objects import unrotated
at scale 1. Nobody sets these options by hand: change them here.
"""
import argparse
import os
import re
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_cli  # noqa: E402

LOD_COLLECTION = re.compile(r"^LOD[0-9]$")

FBX_OPTIONS = dict(
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=False,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    use_tspace=False,
    colors_type="LINEAR",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=True,
    armature_nodetype="NULL",
    path_mode="STRIP",
    embed_textures=False,
    use_custom_props=False,
)


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def rigs_of(meshes):
    rigs = []
    for mesh in meshes:
        for modifier in mesh.modifiers:
            if modifier.type == "ARMATURE" and modifier.object and modifier.object not in rigs:
                rigs.append(modifier.object)
    return rigs


def export_fbx(path, objects, with_animation):
    select_only(objects)
    bpy.ops.export_scene.fbx(
        filepath=path,
        bake_anim=with_animation,
        bake_anim_use_all_actions=with_animation,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        **FBX_OPTIONS,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", required=True, help="output folder")
    args = blender_cli.parse_args(parser)

    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    asset = blender_cli.blend_name()
    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    written = {"asset": asset, "source": os.path.relpath(bpy.data.filepath, blender_cli.REPO_ROOT).replace("\\", "/"),
               "blender": bpy.app.version_string, "lods": [], "animations": None}

    lod_collections = sorted((c for c in bpy.data.collections if LOD_COLLECTION.match(c.name)), key=lambda c: c.name)
    if not lod_collections:
        print(f"export: {asset} has no LOD0..LOD9 collections; nothing to export", file=sys.stderr)
        sys.exit(1)

    for collection in lod_collections:
        meshes = [o for o in collection.all_objects if o.type == "MESH"]
        objects = meshes + rigs_of(meshes)
        path = os.path.join(out, f"{asset}_{collection.name}.fbx")
        export_fbx(path, objects, with_animation=False)
        written["lods"].append({"lod": collection.name, "file": os.path.basename(path),
                                "objects": sorted(o.name for o in objects)})
        print(f"export: wrote {path}")

    lod0_meshes = [o for o in lod_collections[0].all_objects if o.type == "MESH"]
    rigs = rigs_of(lod0_meshes)
    actions = sorted(a.name for a in bpy.data.actions)
    if rigs and actions:
        path = os.path.join(out, f"{asset}_Anims.fbx")
        export_fbx(path, lod0_meshes + rigs, with_animation=True)
        written["animations"] = {"file": os.path.basename(path), "clips": actions}
        print(f"export: wrote {path} ({len(actions)} clips)")

    blender_cli.write_json(os.path.join(out, "export.json"), written)


if __name__ == "__main__":
    main()
