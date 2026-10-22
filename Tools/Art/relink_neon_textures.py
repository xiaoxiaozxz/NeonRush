"""Run inside Blender to repair and verify the source file's migrated texture paths."""
from pathlib import Path
import json
import bpy

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/NeonHarbor/LoongVela_NeonHarbor.blend"
TEXTURES = ROOT / "NeonRush/Assets/_Project/Art/NeonHarbor/Textures"

if Path(bpy.data.filepath).resolve() != SOURCE.resolve():
    raise RuntimeError("Open the NeonHarbor source file before relinking its textures.")

images = []
changed = False
for image in bpy.data.images:
    if image.source != "FILE" or not image.filepath:
        continue
    old = image.filepath
    normalized = old.replace("\\", "/")
    if "/Assets/LoongRacing/" in normalized:
        target = TEXTURES / normalized.rsplit("/", 1)[-1]
        if not target.is_file():
            raise FileNotFoundError(target)
        image.filepath = bpy.path.relpath(str(target))
        changed = True
    resolved = Path(bpy.path.abspath(image.filepath)).resolve()
    if not resolved.is_file() and not image.packed_file:
        raise FileNotFoundError(resolved)
    images.append({"name": image.name, "previous": old, "current": image.filepath, "exists": resolved.is_file()})

if changed:
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE), compress=True)
report = ROOT / "NeonRush/Captures/ProjectStructure/blender-textures.json"
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({"allTexturesResolved": True, "images": images}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Verified {len(images)} Blender source textures; changed={changed}.")
