import sys
from collections import Counter
from pathlib import Path

import bpy


source = Path(sys.argv[sys.argv.index("--") + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source.resolve()))
mesh_object = next(
    obj
    for obj in bpy.context.scene.objects
    if obj.type == "MESH" and any(mod.type == "ARMATURE" for mod in obj.modifiers)
)
mesh = mesh_object.data
world = mesh_object.matrix_world
coordinates = [world @ vertex.co for vertex in mesh.vertices]
print("BOUNDS", {
    axis: (min(getattr(co, axis) for co in coordinates), max(getattr(co, axis) for co in coordinates))
    for axis in ("x", "y", "z")
})
print("OBJECT", {
    "mesh_location": tuple(mesh_object.location),
    "mesh_rotation": tuple(mesh_object.rotation_euler),
    "mesh_scale": tuple(mesh_object.scale),
})
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
print("ARMATURE", {
    "location": tuple(armature.location),
    "rotation": tuple(armature.rotation_euler),
    "scale": tuple(armature.scale),
    "dimensions": tuple(armature.dimensions),
})
print("GROUPS", Counter(group.name for group in mesh_object.vertex_groups))
for axis in ("x", "y", "z"):
    values = sorted(getattr(co, axis) for co in coordinates)
    print("PERCENTILES", axis, [round(values[int((len(values) - 1) * p)], 4) for p in (0, .1, .25, .5, .75, .9, 1)])
