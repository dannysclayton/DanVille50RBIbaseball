import sys
from pathlib import Path

import bpy


source = Path(sys.argv[sys.argv.index("--") + 1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source.resolve()))
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
action = armature.animation_data.action
print("ACTION", action.name)
print("SLOT_ATTRS", [name for name in dir(action.slots[0]) if not name.startswith("_")])
print("SLOTS", [(slot.identifier, slot.target_id_type) for slot in action.slots])
print("ACTIVE_SLOT", armature.animation_data.action_slot.identifier if armature.animation_data.action_slot else None)
print("NLA", [(track.name, [strip.name for strip in track.strips]) for track in armature.animation_data.nla_tracks])
