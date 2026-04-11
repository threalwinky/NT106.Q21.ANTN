# Netrix Architecture

## Modules

- `client/client`
  - WinForms UI
  - themed Windows UI
  - connection management
  - DXGI Desktop Duplication capture with GDI fallback
  - JPEG frame streaming
  - remote mouse/keyboard forwarding
  - AES-GCM room payload encryption
  - basic file transfer UI and download handling
  - internet auth and load-balancer calls
- `java/netrix-java-client`
  - Swing UI for Linux or JVM desktops
  - same room and WebSocket protocol as the C# client
  - secure room payload support
  - AWT Robot based screen capture and remote input
- `main-server`
  - FastAPI WebSocket server
  - room lifecycle and password validation
  - frame relay, input relay, chat relay
  - secure payload relay
  - file transfer relay
- `auth-server`
  - register/login
  - bcrypt password hashing
  - JWT issuance
  - session tracking in SQLite
- `load-balancer`
  - checks main server health
  - room-aware server selection
  - dashboard endpoint
  - returns a tunnel-friendly WebSocket endpoint
- `shared/netrix_shared`
  - JWT config and helper functions
  - password hashing and verification helpers

## LAN flow

1. Host connects directly to `main-server`
2. Host creates a room with a password
3. Controller or viewer connects to the same `main-server`
4. Client joins with `room_id + password`
5. Host streams frames, controller sends input, server relays both

## Internet flow

1. Client authenticates against `auth-server`
2. Client sends JWT to `load-balancer`
3. `load-balancer` selects a healthy `main-server`
4. Client connects to the selected `ws_url`
5. Client creates or joins a room with `room_id + password + JWT`
6. When reconnecting to an existing room, `room_id` can keep the client on the same main server via room affinity

## Message contract

Text WebSocket messages are JSON with these primary types:

- `create_room`
- `join_room`
- `frame`
- `input`
- `chat`
- `file_offer`
- `file_chunk`
- `file_complete`
- `secure_payload`
- `ping`

Server responses/events:

- `hello`
- `room_created`
- `joined_room`
- `room_state`
- `frame`
- `input`
- `chat`
- `pong`
- `error`
- `room_closed`
