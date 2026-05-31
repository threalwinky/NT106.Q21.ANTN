# Netrix 1.0.6 Architecture

Netrix 1.0.6 is a remote-control system with a C# WinForms client and Python servers. The server stack is split into `auth-server`, `load-balancer`, and multiple `main-server` nodes.

The main change in 1.0.6 is transport: main-server realtime traffic no longer uses WebSocket. Control plane and data plane both use raw TCP sockets with a 4-byte big-endian length prefix.

## Components

| Component | Responsibility |
|---|---|
| C# client | Login, create/join room, screen capture, render, input, chat, file transfer, clipboard sync |
| `auth-server` | Register/login/logout, JWT, PostgreSQL users and sessions |
| `load-balancer` | Auth proxy, main-server health checks, room affinity, returns `tcp_endpoint` |
| `main-server` | TCP room realtime, room validation, frame/input/chat/file/clipboard relay |
| `shared` | JWT/security/config helpers for Python services |

## Network Ports

Public client-facing ports:

| Service | Port | Protocol |
|---|---:|---|
| Load balancer | `10310` | HTTP |
| Main server 1 | `10311` | Raw TCP realtime |
| Main server 2 | `10312` | Raw TCP realtime |
| Main server 3 | `10313` | Raw TCP realtime |

Internal service ports:

| Service | Port | Use |
|---|---:|---|
| Auth server | `8001` | Internal HTTP; exposed through `/auth/*` on LB |
| Load balancer | `8002` | Container HTTP |
| Main server 1 | `8000` | `/health` only |
| Main server 2 | `8003` | `/health` only |
| Main server 3 | `8004` | `/health` only |
| PostgreSQL | `5433` | Internal auth database |

## Realtime TCP Protocol

Every TCP packet starts with a 4-byte signed big-endian payload length.

Payload types:

| Payload | Meaning |
|---|---|
| UTF-8 JSON | Control plane: `create_room`, `join_room`, `input`, `chat`, `file_*`, `secure_payload`, `ping` |
| Binary starting `NXF1` | Encrypted screen frame packet from host |

Main server does not decrypt screen frames. It validates sender role, then relays the encrypted packet to the room peer.

## Internet Flow

```text
C# client
  -> HTTP /auth/login on load-balancer
  -> HTTP /select-server on load-balancer
  <- tcp_endpoint, for example 45.122.249.68:10311
  -> TCP connect to selected main-server
  -> JSON create_room / join_room
  -> JSON control + binary NXF1 frames on same TCP socket
```

## LAN Flow

In LAN mode the client derives the load balancer URL from the entered server IP:

```text
http://{server-ip}:10310
```

Auth still goes through `/auth/*`, and main-server selection still goes through `/select-server`. For LAN testing, configure `NETRIX_MAIN*_PUBLIC_TCP_ENDPOINT` to the LAN IP and set `NETRIX_ALLOW_PRIVATE_TCP_ENDPOINTS=true`.

## Room Rules

1. User must have a valid JWT.
2. Room requires password in LAN and Internet mode.
3. Only `host` and `controller` roles exist.
4. A room currently allows one host and one controller.
5. Controller can view after join but cannot send input until the host approves.
6. Host disconnect closes the room.

## Main Server Modules

| Module | Responsibility |
|---|---|
| `netrix_main/app_factory.py` | Creates FastAPI health app, state, and service |
| `netrix_main/routes.py` | HTTP `/health` only |
| `netrix_main/realtime_tcp.py` | Raw TCP realtime server and message dispatch |
| `netrix_main/service.py` | Room state, participant validation, broadcast helpers |
| `netrix_main/models.py` | `Room` and `Participant` dataclasses |
| `netrix_main/store.py` | In-memory runtime state |

## Security

JWT is mandatory for room creation and join. Room password is mandatory and is also used by the client to derive encryption keys for secure payloads. Main server validates role and controller approval before relaying input. PostgreSQL stays private to the Docker network; public clients only need `10310-10313/TCP`.

## Current Limits

Room state is in memory, so rooms do not migrate between main-server nodes. Current room capacity is one host and one controller. Public raw TCP must be protected by firewall rules or deployed behind a TCP-capable tunnel/proxy when Internet exposure is needed.
