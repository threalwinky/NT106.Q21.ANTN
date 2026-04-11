from __future__ import annotations

import os

from netrix_main.app_factory import create_app


app = create_app()


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "app:app",
        host=os.getenv("NETRIX_MAIN_HOST", "0.0.0.0"),
        port=int(os.getenv("NETRIX_MAIN_PORT", "8000")),
        reload=False,
    )
