from __future__ import annotations

import sys
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[2]
SHARED_PY_DIR = ROOT_DIR / "shared" / "py"

if str(SHARED_PY_DIR) not in sys.path:
    sys.path.append(str(SHARED_PY_DIR))

