import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def point_camera(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


output = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 960
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("PreviewWorld")
scene.world.color = (0.025, 0.035, 0.05)

for material_name, color in {
    "DRBI_Jersey": (0.92, 0.94, 0.97, 1.0),
    "DRBI_Pants": (0.12, 0.21, 0.40, 1.0),
    "DRBI_Cap": (0.72, 0.08, 0.10, 1.0),
    "DRBI_Accent": (0.98, 0.72, 0.08, 1.0),
}.items():
    material = bpy.data.materials.get(material_name)
    if material:
        material.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = color

bpy.context.scene.frame_set(1)

bpy.ops.object.camera_add(location=(0.0, -4.3, 1.0))
camera = bpy.context.object
camera.data.lens = 58
point_camera(camera, Vector((0.0, 0.0, 0.95)))
scene.camera = camera

bpy.ops.object.light_add(type="AREA", location=(-2.0, -3.0, 3.2))
bpy.context.object.data.energy = 950
bpy.context.object.data.shape = "DISK"
bpy.context.object.data.size = 4.0
bpy.ops.object.light_add(type="AREA", location=(2.5, -1.0, 1.6))
bpy.context.object.data.energy = 650
bpy.context.object.data.size = 3.0
bpy.ops.object.light_add(type="AREA", location=(0.0, 2.0, 2.6))
bpy.context.object.data.energy = 800
bpy.context.object.data.size = 2.0

bpy.ops.mesh.primitive_plane_add(size=12, location=(0.0, 0.0, -0.01))
ground = bpy.context.object
material = bpy.data.materials.new("PreviewGround")
material.diffuse_color = (0.025, 0.08, 0.04, 1.0)
ground.data.materials.append(material)

output.parent.mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)
