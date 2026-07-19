"""Build Dan's RBI Baseball 2026 runtime character assets in Blender.

Run with Blender, not the system Python interpreter. The script keeps the
Meshy skin/face detail, replaces generated uniform lettering with named flat
materials, and writes a skinned base GLB plus lightweight animation GLBs.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter
from pathlib import Path

import bpy
from mathutils import Vector


MATERIAL_JERSEY = "DRBI_Jersey"
MATERIAL_PANTS = "DRBI_Pants"
MATERIAL_CAP = "DRBI_Cap"
MATERIAL_ACCENT = "DRBI_Accent"
MATERIAL_SKIN_DETAIL = "DRBI_SkinDetail"


def parse_arguments() -> argparse.Namespace:
    argv = sys.argv
    blender_separator = argv.index("--") if "--" in argv else len(argv)
    parser = argparse.ArgumentParser()
    parser.add_argument("--pitch", required=True, type=Path)
    parser.add_argument("--run", required=True, type=Path)
    parser.add_argument("--walk", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--blend", required=True, type=Path)
    return parser.parse_args(argv[blender_separator + 1 :])


def reset_and_import(source: Path) -> tuple[bpy.types.Object, bpy.types.Object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source.resolve()))

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    skinned_meshes = [
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and any(modifier.type == "ARMATURE" for modifier in obj.modifiers)
    ]
    if len(armatures) != 1 or len(skinned_meshes) != 1:
        raise RuntimeError(
            f"Expected one armature and one skinned mesh in {source}; "
            f"found {len(armatures)} armatures and {len(skinned_meshes)} skinned meshes."
        )

    armature = armatures[0]
    mesh = skinned_meshes[0]
    for obj in list(bpy.context.scene.objects):
        if obj not in (armature, mesh):
            bpy.data.objects.remove(obj, do_unlink=True)
    armature.name = "DRBI_PlayerArmature"
    mesh.name = "DRBI_PlayerMesh"
    return armature, mesh


def active_action(armature: bpy.types.Object) -> bpy.types.Action:
    action = armature.animation_data.action if armature.animation_data else None
    if action is None:
        raise RuntimeError("Imported armature does not have an active animation action.")
    return action


def principled_material(name: str, color: tuple[float, float, float, float]) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = 0.72
    shader.inputs["Metallic"].default_value = 0.0
    return material


def texture_image(material: bpy.types.Material) -> bpy.types.Image:
    if not material.use_nodes:
        raise RuntimeError("Imported material does not use nodes.")
    for node in material.node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image is not None:
            return node.image
    raise RuntimeError("Imported material does not contain a base-color image texture.")


def sample_texture(
    pixels: list[float], width: int, height: int, u: float, v: float
) -> tuple[float, float, float]:
    x = min(width - 1, max(0, int((u % 1.0) * width)))
    y = min(height - 1, max(0, int((v % 1.0) * height)))
    offset = (y * width + x) * 4
    return pixels[offset], pixels[offset + 1], pixels[offset + 2]


def classify_color(rgb: tuple[float, float, float]) -> str:
    red, green, blue = rgb
    maximum = max(rgb)
    minimum = min(rgb)
    if red > 0.20 and red > green * 1.28 and red > blue * 1.45:
        return "skin"
    if minimum > 0.52 and maximum - minimum < 0.28:
        return "white"
    if green > 0.055 and green > red * 1.20 and green > blue * 1.06:
        return "green"
    return "detail"


def polygon_average_color(
    mesh: bpy.types.Mesh,
    polygon: bpy.types.MeshPolygon,
    pixels: list[float],
    width: int,
    height: int,
) -> tuple[float, float, float]:
    uv_layer = mesh.uv_layers.active
    if uv_layer is None:
        return 1.0, 1.0, 1.0
    samples = []
    for loop_index in polygon.loop_indices:
        uv = uv_layer.data[loop_index].uv
        samples.append(sample_texture(pixels, width, height, uv.x, uv.y))
    if not samples:
        return 1.0, 1.0, 1.0
    count = float(len(samples))
    return tuple(sum(sample[channel] for sample in samples) / count for channel in range(3))


def polygon_height(
    mesh_object: bpy.types.Object, polygon: bpy.types.MeshPolygon
) -> float:
    mesh = mesh_object.data
    vertices = [
        (mesh_object.matrix_world @ mesh.vertices[index].co).z
        for index in polygon.vertices
    ]
    return sum(vertices) / max(1, len(vertices))


def polygon_dominant_group(
    mesh_object: bpy.types.Object, polygon: bpy.types.MeshPolygon
) -> str:
    totals: Counter[int] = Counter()
    for vertex_index in polygon.vertices:
        for membership in mesh_object.data.vertices[vertex_index].groups:
            totals[membership.group] += membership.weight
    if not totals:
        return ""
    group_index, _ = totals.most_common(1)[0]
    return mesh_object.vertex_groups[group_index].name


def replace_uniform_materials(mesh_object: bpy.types.Object) -> Counter[str]:
    imported_material = mesh_object.material_slots[0].material
    image = texture_image(imported_material)
    width, height = image.size
    pixels = list(image.pixels)

    materials = [
        principled_material(MATERIAL_JERSEY, (1.0, 1.0, 1.0, 1.0)),
        principled_material(MATERIAL_PANTS, (0.88, 0.88, 0.90, 1.0)),
        principled_material(MATERIAL_CAP, (1.0, 1.0, 1.0, 1.0)),
        principled_material(MATERIAL_ACCENT, (0.12, 0.12, 0.14, 1.0)),
        imported_material.copy(),
    ]
    materials[-1].name = MATERIAL_SKIN_DETAIL
    mesh_object.data.materials.clear()
    for material in materials:
        mesh_object.data.materials.append(material)

    material_index = {
        MATERIAL_JERSEY: 0,
        MATERIAL_PANTS: 1,
        MATERIAL_CAP: 2,
        MATERIAL_ACCENT: 3,
        MATERIAL_SKIN_DETAIL: 4,
    }
    counts: Counter[str] = Counter()
    for polygon in mesh_object.data.polygons:
        color_class = classify_color(
            polygon_average_color(mesh_object.data, polygon, pixels, width, height)
        )
        height_value = polygon_height(mesh_object, polygon)
        group = polygon_dominant_group(mesh_object, polygon)

        if group in ("LeftFoot", "LeftToeBase", "RightFoot", "RightToeBase"):
            assignment = MATERIAL_SKIN_DETAIL
        elif group in ("LeftHand", "RightHand", "neck"):
            assignment = MATERIAL_SKIN_DETAIL
        elif group in ("Head", "head_end", "headfront"):
            assignment = (
                MATERIAL_CAP
                if height_value > 1.60 and color_class != "skin"
                else MATERIAL_SKIN_DETAIL
            )
        elif group in ("LeftForeArm", "RightForeArm"):
            assignment = MATERIAL_ACCENT
        elif group in ("LeftUpLeg", "LeftLeg", "RightUpLeg", "RightLeg", "Hips"):
            if height_value < 0.42:
                assignment = MATERIAL_ACCENT
            elif group == "Hips" and color_class == "green" and height_value > 0.82:
                assignment = MATERIAL_ACCENT
            else:
                assignment = MATERIAL_PANTS
        elif group in (
            "Spine",
            "Spine01",
            "Spine02",
            "LeftShoulder",
            "RightShoulder",
            "LeftArm",
            "RightArm",
        ):
            assignment = MATERIAL_JERSEY
        elif height_value < 0.18:
            assignment = MATERIAL_SKIN_DETAIL
        elif height_value < 0.88:
            assignment = MATERIAL_PANTS
        else:
            assignment = MATERIAL_JERSEY

        polygon.material_index = material_index[assignment]
        counts[assignment] += 1
    return counts


def select_only(*objects: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def export_glb(path: Path, *objects: bpy.types.Object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    select_only(*objects)
    bpy.ops.export_scene.gltf(
        filepath=str(path.resolve()),
        check_existing=False,
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_merge_animation="NONE",
        export_reset_pose_bones=True,
        export_skins=True,
        export_def_bones=True,
        export_leaf_bone=False,
        export_force_sampling=True,
        export_frame_range=True,
        export_frame_step=1,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_extras=True,
        export_cameras=False,
        export_lights=False,
        export_yup=True,
    )


def build_base(source: Path, output: Path, blend_path: Path) -> None:
    armature, mesh = reset_and_import(source)
    action = active_action(armature)
    action.name = "Pitch_R"
    for slot in action.slots:
        slot.name_display = "Pitch_R"
    for track in armature.animation_data.nla_tracks:
        track.name = "Pitch_R"
        for strip in track.strips:
            strip.name = "Pitch_R"
    counts = replace_uniform_materials(mesh)
    armature["drbi_character_version"] = 1
    mesh["drbi_uniform_materials"] = ",".join(
        (MATERIAL_JERSEY, MATERIAL_PANTS, MATERIAL_CAP, MATERIAL_ACCENT)
    )
    export_glb(output, armature, mesh)
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path.resolve()))
    print("DRBI_UNIFORM_POLYGONS", dict(counts))


def build_animation(source: Path, output: Path, action_name: str) -> None:
    armature, mesh = reset_and_import(source)
    bpy.data.objects.remove(mesh, do_unlink=True)
    action = active_action(armature)
    action.name = action_name
    for slot in action.slots:
        slot.name_display = action_name
    for track in armature.animation_data.nla_tracks:
        track.name = action_name
        for strip in track.strips:
            strip.name = action_name
    export_glb(output, armature)


def main() -> None:
    arguments = parse_arguments()
    arguments.output_dir.mkdir(parents=True, exist_ok=True)
    build_base(
        arguments.pitch,
        arguments.output_dir / "player_base.glb",
        arguments.blend,
    )
    build_animation(arguments.run, arguments.output_dir / "player_run.glb", "Run")
    build_animation(arguments.walk, arguments.output_dir / "player_walk.glb", "Walk")
    print("DRBI_BUILD_COMPLETE", arguments.output_dir.resolve())


if __name__ == "__main__":
    main()
