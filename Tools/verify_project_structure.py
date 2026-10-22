"""Verify the GUID-preserving Assets migration without starting Unity."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "NeonRush/Assets"
REPORTS = ROOT / "NeonRush/Captures/ProjectStructure"


def main():
    before = json.loads((REPORTS / "before-assets.json").read_text(encoding="utf-8-sig"))
    current = {}
    for path in ASSETS.rglob("*"):
        if path.suffix == ".meta":
            continue
        if not Path(str(path) + ".meta").exists():
            raise AssertionError(f"Missing metadata: {path}")
    for meta in ASSETS.rglob("*.meta"):
        asset = Path(str(meta)[:-5])
        assert asset.exists(), f"Orphan metadata: {meta}"
        match = re.search(r"(?m)^guid: ([0-9a-f]{32})", meta.read_text(encoding="utf-8-sig"))
        assert match, f"Missing GUID: {meta}"
        guid = match.group(1)
        assert guid not in current, f"Duplicate GUID: {guid}"
        current[guid] = {
            "path": asset.relative_to(ASSETS).as_posix(),
            "metaHash": hashlib.sha256(meta.read_bytes()).hexdigest().upper(),
        }
    for item in before:
        assert item["guid"] in current, f"Lost GUID: {item['path']}"
        assert current[item["guid"]]["metaHash"] == item["metaHash"], f"Changed importer metadata: {item['path']}"
    assert {p.name for p in ASSETS.iterdir() if p.is_dir()} == {"_Project", "ThirdParty"}
    core = ASSETS / "_Project/Scripts/Runtime/Core"
    assert json.loads((core / "LoongRacing.Core.asmdef").read_text())["noEngineReferences"]
    for script in core.glob("*.cs"):
        assert not re.search(r"\b(UnityEngine|UnityEditor|MonoBehaviour)\b", script.read_text()), script
    stale = re.compile(r"Assets/(LoongRacing|Scenes|Settings|TutorialInfo)(?:/|\b)")
    paths = [*ASSETS.rglob("*.cs"), ROOT / "Tools/generate_kart_audio.py", ROOT / "Tools/Art/build_neon_assets.py",
             ROOT / "NeonRush/ProjectSettings/EditorBuildSettings.asset", ROOT / "NeonRush/ProjectSettings/ProjectSettings.asset"]
    for path in paths:
        assert not stale.search(path.read_text(encoding="utf-8-sig")), f"Stale asset path: {path}"
    report = {
        "preservedExistingGuids": len(before), "totalGuids": len(current),
        "unchangedExistingImporterMetadata": True, "coreHasNoEngineReferences": True,
        "oldActivePathsRemaining": 0,
        "moves": [{"guid": item["guid"], "from": item["path"], "to": current[item["guid"]]["path"]} for item in before],
    }
    (REPORTS / "migration-verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"PASS: {len(before)} existing GUIDs and importer metadata preserved; {len(current)} total GUIDs.")


if __name__ == "__main__":
    main()
