from __future__ import annotations

import argparse
import os

from netrix_main.app_factory import create_app


app = create_app()


def read_bool(name: str, default: bool = False) -> bool:
    raw_value = os.getenv(name)
    if raw_value is None:
        return default
    return raw_value.strip().lower() in {"1", "true", "yes", "on"}


if __name__ == "__main__":
    import uvicorn

    parser = argparse.ArgumentParser(description="Run a Netrix main server node.")
    parser.add_argument("port_value", nargs="?", type=int, help="Port to bind, for example 8003.")
    parser.add_argument("--port", dest="port_override", type=int, help="Port to bind, for example 8003.")
    parser.add_argument("--host", default=os.getenv("NETRIX_MAIN_HOST", "0.0.0.0"), help="Host to bind.")
    parser.add_argument("--server-name", dest="server_name", help="Optional NETRIX_SERVER_NAME override.")
    parser.add_argument("--public-ws-url", dest="public_ws_url", help="Optional NETRIX_PUBLIC_WS_URL override.")
    args = parser.parse_args()

    if args.server_name:
        os.environ["NETRIX_SERVER_NAME"] = args.server_name

    if args.public_ws_url:
        os.environ["NETRIX_PUBLIC_WS_URL"] = args.public_ws_url

    selected_port = args.port_override or args.port_value or int(os.getenv("NETRIX_MAIN_PORT", "8000"))

    uvicorn.run(
        app,
        host=args.host,
        port=selected_port,
        reload=False,
        access_log=read_bool("NETRIX_ACCESS_LOG"),
    )
