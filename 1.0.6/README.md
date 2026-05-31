## Netrix
Version: 1.0.6

Netrix 1.0.6 switches the main-server realtime path to raw TCP sockets for both control plane and data plane.

1. Client auth/register/login goes through the load balancer proxy at `http://45.122.249.68:10310/auth`.
2. The load balancer returns `tcp_endpoint`, not `ws_url`.
3. Main-server control messages use length-prefixed UTF-8 JSON packets over TCP.
4. Screen frames use length-prefixed binary packets with magic header `NXF1` over the same TCP socket.
5. Rooms support only `host` and `controller`; controller input is blocked until the host approves it.
6. Clipboard sync and file transfer use encrypted `secure_payload` messages over TCP.
7. Host screen capture runs on a dedicated C# `Thread`; Python servers keep async socket handling.

## Docker Compose

Run the full server stack from this folder:

```bash
docker-compose up -d --build
docker-compose ps
```

Public client-facing ports:

| Service | Host port | Notes |
|---|---:|---|
| `load-balancer` | `10310` | HTTP API plus `/auth/*` proxy |
| `main-server-1` | `10311` | Raw TCP realtime |
| `main-server-2` | `10312` | Raw TCP realtime |
| `main-server-3` | `10313` | Raw TCP realtime |

Internal ports kept for container health checks:

| Service | Internal port |
|---|---:|
| `auth-server` | `8001` |
| `load-balancer` | `8002` |
| `main-server-1` HTTP health | `8000` |
| `main-server-2` HTTP health | `8003` |
| `main-server-3` HTTP health | `8004` |
| `cloud` PostgreSQL | `5433` |

The default `.env` targets public IP `45.122.249.68`:

```env
NETRIX_LB_PORT=10310
NETRIX_MAIN1_PORT=10311
NETRIX_MAIN2_PORT=10312
NETRIX_MAIN3_PORT=10313
NETRIX_MAIN1_PUBLIC_TCP_ENDPOINT=45.122.249.68:10311
NETRIX_MAIN2_PUBLIC_TCP_ENDPOINT=45.122.249.68:10312
NETRIX_MAIN3_PUBLIC_TCP_ENDPOINT=45.122.249.68:10313
```

For LAN-only testing, set each `NETRIX_MAIN*_PUBLIC_TCP_ENDPOINT` to the LAN IP and allow private endpoints:

```bash
NETRIX_MAIN1_PUBLIC_TCP_ENDPOINT=192.168.1.50:10311 \
NETRIX_MAIN2_PUBLIC_TCP_ENDPOINT=192.168.1.50:10312 \
NETRIX_MAIN3_PUBLIC_TCP_ENDPOINT=192.168.1.50:10313 \
NETRIX_ALLOW_PRIVATE_TCP_ENDPOINTS=true \
docker-compose up -d --build
```

Open firewall ports `10310-10313/TCP` for clients. PostgreSQL is bound to localhost only and should not be exposed publicly.

## Client Flow

1. User signs in or registers via load balancer `/auth`.
2. Client calls `/select-server` with JWT.
3. Load balancer checks main-server `/health` and returns `tcp_endpoint`.
4. Client opens `TcpClient` to that endpoint.
5. Create/join room messages are JSON packets.
6. Encrypted H.264/JPEG frames are binary `NXF1` packets.

## Running Individual Main Server

```bash
python3 app.py --port 8000 --server-name main-server-1 --realtime-tcp-port 9000 --public-tcp-endpoint 45.122.249.68:10311
```

The HTTP port is only for `/health`; realtime room traffic is on the TCP port.
