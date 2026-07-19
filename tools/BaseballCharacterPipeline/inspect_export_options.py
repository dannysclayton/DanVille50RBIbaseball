import bpy

properties = bpy.ops.export_scene.gltf.get_rna_type().properties
for name in ("export_animation_mode", "export_nla_strips", "export_merge_animation", "export_reset_pose_bones"):
    prop = properties.get(name)
    if prop is None:
        continue
    print("PROPERTY", name, prop.description)
    if hasattr(prop, "enum_items"):
        print("ITEMS", [(item.identifier, item.name, item.description) for item in prop.enum_items])
