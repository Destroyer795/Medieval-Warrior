"""Export the verified village passages for runtime Horde hazards (stdlib only)."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = json.loads((root / "ArtSource/Buildings/slope-audit.json").read_text())
lanes = []
for lane in source["lanes"]:
    def point(key):
        return {"x": lane[key]["x"], "y": lane["steepest"]["y"], "z": lane[key]["y"]}
    lanes.append({"start": point("start"), "end": point("end"), "width": lane["width"]})
target = root / "Assets/Resources/Horde/Arena.json"
target.parent.mkdir(parents=True, exist_ok=True)
parts = json.loads((root / "Tools/ArtPipeline/Buildings/manifest.json").read_text())["parts"]
bridges = [part["key"] for part in parts if part["name"].startswith("Bridge")]
target.write_text(json.dumps({"lanes": lanes, "bridgeKeys": bridges}, indent=2) + "\n")
print(f"Exported {len(lanes)} verified passages to {target}")
