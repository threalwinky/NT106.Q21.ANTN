# Netrix Architecture

## Modules

- `client/client`
  - WinForms UI
  - connection management
  - screen capture and JPEG streaming
  - remote mouse/keyboard forwarding
  - internet auth and load-balancer calls
- `main-server`
  - FastAPI WebSocket server
  - room lifecycle and password validation
  - frame relay, input relay, chat relay
- `auth-server`
  - register/login
  - bcrypt password hashing
  - JWT issuance
  - session tracking in SQLite
- `load-balancer`
  - checks main server health
  - selects the least-loaded healthy node
  - returns a tunnel-friendly WebSocket endpoint
- `shared/py/netrix_shared`
  - JWT config and helper functions
  - password hashing and verification helpers

## LAN flow

1. Host connects directly to `main-server`
2. Host creates a room with a password
3. Controller/viewer connects to the same `main-server`
4. Client joins with `room_id + password`
5. Host streams frames, controller sends input, server relays both

## Internet flow

1. Client authenticates against `auth-server`
2. Client sends JWT to `load-balancer`
3. `load-balancer` selects a healthy `main-server`
4. Client connects to the selected `ws_url`
5. Client creates or joins a room with `room_id + password + JWT`

## Message contract

Text WebSocket messages are JSON with these primary types:

- `create_room`
- `join_room`
- `frame`
- `input`
- `chat`
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
