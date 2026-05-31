from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parents[1]
sys.path.append(str(ROOT_DIR / "shared"))

from netrix_shared.config import read_bool  # noqa: E402
from netrix_main.app_factory import create_app  # noqa: E402
from netrix_main.realtime_tcp import start_realtime_tcp_server  # noqa: E402


app = create_app()


if __name__ == "__main__":
    import uvicorn

    parser = argparse.ArgumentParser(description="Run a Netrix main server node.")
    parser.add_argument("port_value", nargs="?", type=int, help="Port to bind, for example 8003.")
    parser.add_argument("--port", dest="port_override", type=int, help="Port to bind, for example 8003.")
    parser.add_argument("--host", default=os.getenv("NETRIX_MAIN_HOST", "0.0.0.0"), help="Host to bind.")
    parser.add_argument("--server-name", dest="server_name", help="Optional NETRIX_SERVER_NAME override.")
    parser.add_argument("--realtime-tcp-port", dest="realtime_tcp_port", type=int, help="Port for raw TCP realtime control and data.")
    parser.add_argument("--public-tcp-endpoint", dest="public_tcp_endpoint", help="Optional NETRIX_PUBLIC_TCP_ENDPOINT override.")
    args = parser.parse_args()

    if args.server_name:
        os.environ["NETRIX_SERVER_NAME"] = args.server_name

    if args.public_tcp_endpoint:
        os.environ["NETRIX_PUBLIC_TCP_ENDPOINT"] = args.public_tcp_endpoint

    selected_port = args.port_override or args.port_value or int(os.getenv("NETRIX_MAIN_PORT", "8000"))
    selected_realtime_tcp_port = args.realtime_tcp_port or int(os.getenv("NETRIX_REALTIME_TCP_PORT", "9000"))

    async def run_http_and_tcp() -> None:
        config = uvicorn.Config(
            app,
            host=args.host,
            port=selected_port,
            reload=False,
            access_log=read_bool("NETRIX_ACCESS_LOG"),
        )
        server = uvicorn.Server(config)
        realtime_server = await start_realtime_tcp_server(
            app.state.netrix_state,
            app.state.netrix_room_service,
            args.host,
            selected_realtime_tcp_port,
        )
        realtime_task = asyncio.create_task(realtime_server.serve_forever())
        try:
            await server.serve()
        finally:
            realtime_server.close()
            await realtime_server.wait_closed()
            realtime_task.cancel()
            try:
                await realtime_task
            except asyncio.CancelledError:
                pass

    import asyncio

    asyncio.run(run_http_and_tcp())
