#!/usr/bin/env python3
"""Own-window Mac QA. Requires a GUI login session, no TCC/global input grants."""
import argparse
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app", default=".build/debug/Nashi")
    parser.add_argument("--output", default="build/native-ui")
    args = parser.parse_args()
    app, output = Path(args.app).resolve(), Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)

    def invoke(arguments, expected=0, timeout=120):
        result = subprocess.run([str(app), *map(str, arguments)], capture_output=True,
                                text=True, timeout=timeout)
        if result.returncode != expected:
            raise AssertionError(f"{arguments[0]} returned {result.returncode}, expected {expected}\n"
                                 + result.stdout[-2000:] + result.stderr[-2000:])
        return result

    # The report directory belongs to this test. Guard fixtures are not user data.
    protected = output / "protected-output"
    protected.mkdir(exist_ok=True)
    sentinel = protected / "keep.txt"
    sentinel.write_text("Unrelated files must survive an audit preflight failure.\n")
    before = {p.name: p.read_bytes() for p in protected.iterdir()}
    invoke(["--ui-audit", protected], expected=1)
    assert before == {p.name: p.read_bytes() for p in protected.iterdir()}
    for invalid in (["--scenes", "unknown"], ["--scenes", ""],
                    ["--scenes", "library,library"], ["--appearance", "unknown"],
                    ["--language", "unknown"], ["--scenes"], ["--unknown", "value"]):
        invoke(["--ui-audit", output / "invalid", *invalid], expected=64)
        assert not (output / "invalid").exists()

    invoke(["--ui-audit", output / "gallery"])
    gallery = json.loads((output / "gallery/manifest.json").read_text())
    assert gallery["passed"] and len(gallery["captures"]) == 40
    assert len(gallery["validatorChecks"]) >= 18
    expected = {(s, a, l) for s in ["home.empty", "home.selected", "library",
        "settings.general", "settings.formats", "settings.updates", "wheel.pdf-multiple",
        "editor.crop-image", "editor.annotate-image", "editor.frame-image"]
        for a in ["light", "dark"] for l in ["en", "tr"]}
    assert {(c["scene"], c["appearance"], c["language"]) for c in gallery["captures"]} == expected
    for capture in gallery["captures"]:
        assert capture["status"] == "captured" and not capture["issues"]
        assert (output / "gallery" / capture["image"]).stat().st_size > 100

    invoke(["--range-accessibility-smoke", output / "range"])
    ranges = json.loads((output / "range/mac-range-accessibility.json").read_text())
    assert ranges["passed"] and len(ranges["checks"]) >= 20
    invoke(["--file-artwork-smoke", output / "artwork"])
    artwork = json.loads((output / "artwork/file-artwork.json").read_text())
    assert artwork["passed"] >= 11 and artwork["asyncProviderCalls"] == 1
    print(f"PASS: 40 native UI captures; {len(gallery['validatorChecks'])} audit guardrails; "
          f"{len(ranges['checks'])} range checks; {artwork['passed']} artwork checks; parser/output safety")


if __name__ == "__main__":
    main()
