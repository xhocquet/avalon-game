"""Export PickleKnight to its Godot asset path."""

import runpy
from pathlib import Path

runpy.run_path(str(Path(__file__).with_name("export-hero-to-godot.py")), init_globals={"EXPECTED_HERO": "PickleKnight"})
