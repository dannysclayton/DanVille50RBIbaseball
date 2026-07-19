import json
import struct
import sys
from pathlib import Path


path = Path(sys.argv[1])
data = path.read_bytes()
json_length, json_type = struct.unpack_from("<II", data, 12)
if json_type != 0x4E4F534A:
    raise RuntimeError("First GLB chunk is not JSON")
document = json.loads(data[20 : 20 + json_length].decode("utf-8"))
nodes = document.get("nodes", [])
accessors = document.get("accessors", [])
for animation in document.get("animations", []):
    print("ANIMATION", animation.get("name"), "channels", len(animation.get("channels", [])))
    for channel in animation.get("channels", []):
        target = channel["target"]
        node = nodes[target["node"]].get("name", f"node-{target['node']}")
        sampler = animation["samplers"][channel["sampler"]]
        accessor = accessors[sampler["output"]]
        if target["path"] == "translation" or node in ("Hips", "DRBI_PlayerArmature"):
            print(" ", node, target["path"], accessor.get("min"), accessor.get("max"), accessor.get("count"))
