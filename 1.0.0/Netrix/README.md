# Netrix

Netrix is a room-based remote control system built. It combines a Windows WinForms client with Python services for authentication, room management, and server selection.

## Current implementation

- Secure LAN rooms with `room_id + password`
- Internet flow with `auth-server`, JWT, and `load-balancer`
- `Host`, `Controller`, and `Viewer` roles
- JPEG screen streaming over WebSocket
- Remote mouse and keyboard control
- Room chat

## Stack

- Client: C# WinForms on `.NET 8`
- Servers: Python, FastAPI, WebSocket
- Security: bcrypt password hashing and JWT
- Shared utilities: `shared/py/netrix_shared`

## Repository layout

```text
Netrix/
├── auth-server/
├── client/
├── docs/
├── load-balancer/
├── main-server/
└── shared/
```

## Architecture

- `client/client`
  WinForms UI, connection handling, screen capture, frame rendering, remote input, and internet auth flow.
- `main-server`
  WebSocket room server for create/join room, frame relay, input relay, chat relay, and room lifecycle.
- `auth-server`
  REST API for register, login, JWT issuance, and session validation.
- `load-balancer`
  Chooses a healthy `main-server` and returns its WebSocket endpoint.

More details are available in [docs/architecture.md](docs/architecture.md).

## Requirements

- Windows for the client application
- .NET 8 SDK
- Python 3.10+ for the servers

## Quick start

### 1. Create a virtual environment

Windows `cmd.exe`:

```cmd
python -m venv .venv
.venv\Scripts\activate
pip install -r auth-server\requirements.txt
pip install -r main-server\requirements.txt
pip install -r load-balancer\requirements.txt
```

Linux or macOS:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r auth-server/requirements.txt
pip install -r main-server/requirements.txt
pip install -r load-balancer/requirements.txt
```

### 2. Start the servers

Auth server:

```cmd
python auth-server\app.py
```

Main server for local LAN use:

```cmd
set NETRIX_PUBLIC_WS_URL=ws://127.0.0.1:8000/ws
python main-server\app.py
```

Load balancer:

```cmd
python load-balancer\app.py
```

Default ports:

- `main-server`: `8000`
- `auth-server`: `8001`
- `load-balancer`: `8002`

### 3. Run the client

Build and run on Windows:

```cmd
dotnet build client\client.sln
dotnet run --project client\client\client.csproj
```

You can also open `client/client.sln` in Visual Studio.

## LAN flow

1. Start `main-server` on the host machine.
2. In the host client, set `Mode = LAN`.
3. Set `LAN WebSocket` to `ws://<host-ip>:8000/ws`.
4. Create a room and choose a password.
5. Share `host-ip + room_id + password` with the other machine.
6. On the other machine, join the same room as `Controller` or `Viewer`.

## Internet flow

1. Start `auth-server`, `main-server`, and `load-balancer`.
2. Expose `main-server` with Cloudflare Tunnel or ngrok.
3. Set `NETRIX_PUBLIC_WS_URL` to the public `wss://.../ws` address before starting `main-server`.
4. In the client, switch to `Mode = Internet`.
5. Register or log in to receive a JWT.
6. Create or join a room with the selected server endpoint, room ID, and password.

Example tunnel commands:

```bash
cloudflared tunnel --url http://localhost:8000
ngrok http 8000
```

## How remote control works

- `Host` shares the screen and receives remote input.
- `Controller` can watch, chat, and send mouse and keyboard events.
- `Viewer` can watch and chat only.
- The controller should click once inside `Remote Screen` before sending mouse or keyboard input.

## Notes

- All room access requires `room_id + password`.
- Internet mode also requires a valid JWT.
- The current stream format is JPEG frames over WebSocket, not H.264 or WebRTC.
