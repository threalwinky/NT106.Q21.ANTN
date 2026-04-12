# Netrix 1.0.2 Architecture

## Overview

Netrix là hệ thống remote control theo mô hình client-server.

Kiến trúc của bản `1.0.2` gồm 5 phần chính:

1. `C# Client`
2. `Java Client`
3. `Auth Server`
4. `Load Balancer`
5. `Main Server Cluster`

Mục tiêu của kiến trúc này là:

1. Tách phần xác thực khỏi phần realtime.
2. Tách phần chọn node khỏi phần room/session.
3. Cho phép chạy nhiều `main-server` từ cùng một codebase.
4. Hỗ trợ cả `LAN` và `Internet`.
5. Không cho phép kết nối ẩn danh.

---

## Logical Architecture

```text
                 +----------------------+
                 |     Auth Server      |
                 | register / login     |
                 | bcrypt + JWT         |
                 | SQLite sessions      |
                 +----------+-----------+
                            |
                            v
                 +----------------------+
                 |    Load Balancer     |
                 | health check         |
                 | room affinity        |
                 | failover             |
                 | select ws_url        |
                 +----------+-----------+
                            |
                            v
          +-----------------+-----------------+-----------------+
          |                 |                 |                 |
          v                 v                 v
  +----------------+ +----------------+ +----------------+
  | main-server-1  | | main-server-2  | | main-server-3  |
  | room state     | | room state     | | room state     |
  | frame relay    | | frame relay    | | frame relay    |
  | input relay    | | input relay    | | input relay    |
  | chat relay     | | chat relay     | | chat relay     |
  | file relay     | | file relay     | | file relay     |
  +----------------+ +----------------+ +----------------+
                            ^
                            |
          +-----------------+-----------------+
          |                                   |
          v                                   v
  +----------------------+          +----------------------+
  |   C# Client          |          |   Java Client        |
  | Windows / WinForms   |          | JVM / Swing          |
  +----------------------+          +----------------------+
```

---

## Deployment Architecture

## Internet Mode

```text
User
  |
  v
C# Client / Java Client
  |
  +--> Auth Server
  |      |
  |      +--> issue JWT
  |
  +--> Load Balancer
         |
         +--> query main-server-1 /health
         +--> query main-server-2 /health
         +--> query main-server-3 /health
         |
         +--> return ws_url of selected node
  |
  v
Selected Main Server
  |
  +--> create/join room
  +--> relay frame/input/chat/file
```

## LAN Mode

```text
Host / Controller / Viewer
  |
  v
Client
  |
  +--> user enters LAN Main Server manually
  |
  v
Direct WebSocket connection to a chosen main-server
```

Khác biệt chính:

1. `Internet` đi qua `Auth Server` và `Load Balancer`.
2. `LAN` không đi qua `Load Balancer`.
3. `LAN` vẫn bắt buộc đăng nhập và vẫn dùng `room_id + room_password`.

---

## Main Components

## Client Layer

### `client/client`

Vai trò:

1. Hiển thị UI đăng nhập, đăng ký, dashboard.
2. Cho phép chọn `Internet` hoặc `LAN`.
3. Ở `LAN`, hiển thị ô nhập `LAN Main Server`.
4. Quản lý kết nối WebSocket tới main server.
5. Nếu là host thì capture màn hình và gửi frame JPEG.
6. Nếu là controller thì gửi chuột và bàn phím.
7. Chat và file transfer.
8. Hỗ trợ `secure_payload` theo room password.

### `java/netrix-java-client`

Vai trò:

1. Client JVM cho Linux hoặc môi trường không chạy được WinForms.
2. Dùng cùng room protocol và WebSocket contract với C# client.
3. Hỗ trợ secure room payload.
4. Dùng Swing cho UI và AWT Robot cho input/capture.

---

## Auth Layer

### `auth-server`

Vai trò:

1. `POST /register`
2. `POST /login`
3. `GET /validate`
4. Hash password bằng `bcrypt`
5. Tạo `JWT`
6. Lưu session vào `SQLite`

Auth server không xử lý frame, input hoặc room realtime.

Nó chỉ chịu trách nhiệm xác thực.

---

## Balancing Layer

### `load-balancer`

Vai trò:

1. Đọc danh sách node từ `servers.json`
2. Kiểm tra `/health` của từng node
3. Loại node trả về `ws_url` private hoặc sai định dạng
4. Chọn node còn sống với tải thấp hơn
5. Hỗ trợ `room affinity`
6. Hỗ trợ failover cơ bản khi một node chết

### Registry hiện tại

`servers.json` hiện theo dõi 3 node:

1. `main-server-1` -> `http://127.0.0.1:8000/health`
2. `main-server-2` -> `http://127.0.0.1:8003/health`
3. `main-server-3` -> `http://127.0.0.1:8004/health`

### Public WebSocket URLs

Các node main tương ứng với public endpoint:

1. `main-server-1` -> `wss://main.threalwinky.id.vn/ws`
2. `main-server-2` -> `wss://main2.threalwinky.id.vn/ws`
3. `main-server-3` -> `wss://main3.threalwinky.id.vn/ws`

### Selection Rule

Load balancer hiện chọn node theo:

1. Node phải `healthy`
2. `ws_url` phải hợp lệ và public
3. Nếu có `room_id` và room đó đang nằm trên một node, ưu tiên node đó
4. Nếu không có room affinity, chọn node có `active_connections` và `active_rooms` thấp hơn

---

## Realtime Layer

### `main-server`

Vai trò:

1. Tạo room
2. Join room
3. Validate `room_password`
4. Validate mode `LAN` hoặc `Internet`
5. Validate JWT cho luồng Internet
6. Kiểm tra `network_scope` cho LAN room
7. Relay frame
8. Relay remote input
9. Relay chat
10. Relay file transfer
11. Quản lý controller approval
12. Đóng room khi host disconnect

### Multi-node Design

Mỗi `main-server` là một instance độc lập của cùng codebase.

`main-server/app.py` hỗ trợ chạy nhiều node bằng CLI:

```bash
python3 app.py --port 8000 --server-name main-server-1 --public-ws-url wss://main.threalwinky.id.vn/ws
python3 app.py --port 8003 --server-name main-server-2 --public-ws-url wss://main2.threalwinky.id.vn/ws
python3 app.py --port 8004 --server-name main-server-3 --public-ws-url wss://main3.threalwinky.id.vn/ws
```

Điều này giúp:

1. Không cần copy code sang 3 project khác nhau
2. Dễ dựng cụm main server trên cùng một máy để test
3. Dễ scale ngang nếu triển khai thực tế

### Internal Main Server Modules

| Module | Responsibility |
|---|---|
| `netrix_main/app_factory.py` | Tạo `FastAPI app`, khởi tạo `ServerState`, `RoomService`, route |
| `netrix_main/routes.py` | HTTP + WebSocket protocol, nhận message và gọi service |
| `netrix_main/service.py` | Business logic room/session/broadcast/control approval |
| `netrix_main/models.py` | Dataclass `Room` và `Participant` |
| `netrix_main/store.py` | State runtime trong memory |
| `netrix_main/bootstrap.py` | Chuẩn bị import cho shared package |

---

## Shared Layer

### `shared/netrix_shared`

Vai trò:

1. Đọc JWT config
2. Hash password
3. Verify password
4. Tạo access token
5. Decode access token

Tầng này giúp tránh lặp lại logic security ở `auth-server`, `main-server`, và `load-balancer`.

---

## End-to-End Flows

## Room Creation

1. User đăng nhập
2. Client chọn mode
3. `Internet`:
   - gọi LB để lấy `ws_url`
4. `LAN`:
   - dùng `LAN Main Server` do user nhập
5. Client kết nối tới main server
6. Client gửi `create_room`
7. Main server tạo room hash base32 độ dài 12
8. Host bắt đầu stream frame

## Room Join

1. User đăng nhập
2. User nhập `room_id + room_password`
3. Client kết nối tới main server phù hợp
4. Main server kiểm tra:
   - room tồn tại
   - password đúng
   - mode khớp
   - LAN scope hợp lệ
   - phòng chưa có remote peer khác
5. Nếu join là `controller`, host phải approve

## Screen Streaming

1. Host capture màn hình
2. Nén JPEG
3. Gửi `frame` hoặc `secure_payload(frame)`
4. Main server relay cho peer còn lại
5. Client nhận và render

## Remote Input

1. Controller click vào remote screen để focus
2. Client gửi `mouse` hoặc `keyboard` event
3. Main server chuyển tiếp cho host
4. Host áp dụng input thật trên máy local

---

## Security Architecture

Netrix `1.0.2` có các lớp bảo vệ chính:

1. Bắt buộc đăng nhập trước khi dùng app
2. Room bắt buộc có password
3. Internet mode yêu cầu JWT
4. LAN và Internet không join chéo
5. LAN room bị khóa theo `network_scope`
6. Controller cần host approve
7. Room payload có thể đi qua `secure_payload`

---

## Failover Behavior

### Hiện tại đã có

1. Load balancer đánh dấu node `down` nếu `/health` lỗi
2. Node down sẽ bị loại khỏi danh sách chọn
3. Yêu cầu `select-server` tiếp theo sẽ chuyển sang node còn sống

### Đã test

Trong quá trình test local:

1. Chạy `main-server-1` ở `8000`
2. Chạy `main-server-2` ở `8003`
3. Chạy `main-server-3` ở `8004`
4. Chạy `load-balancer` ở `8002`
5. Khi tắt node `8000`, load balancer vẫn chọn được `main-server-2`

### Giới hạn hiện tại

Failover hiện tại là failover ở mức `node selection`, không phải failover ở mức room state.

Điều đó có nghĩa là:

1. Nếu một node chết trước khi client kết nối, hệ thống vẫn hoạt động
2. Nếu room đang nằm trên node vừa chết, room đó vẫn mất
3. Chưa có shared state hoặc replication giữa các main server

---

## Current Constraints

1. Bản `1.0.2` hiện chỉ hỗ trợ `1 host + 1 remote peer`
2. Room state của main server vẫn lưu trong memory
3. Chưa có session migration giữa các node
4. Client chính thức mạnh nhất vẫn là C# Windows client

---

## Summary

Kiến trúc của Netrix `1.0.2` là kiến trúc nhiều tầng:

1. `Auth Server` lo xác thực
2. `Load Balancer` lo chọn node
3. `Main Server Cluster` lo room và realtime relay
4. `Client` lo UI, capture, render và remote input
5. `Shared Package` lo security helper dùng chung

Đây là một kiến trúc phù hợp để trình bày đồ án vì:

1. Dễ chứng minh tách lớp trách nhiệm
2. Có hỗ trợ cả LAN và Internet
3. Có multi-node ở tầng main server
4. Có failover cơ bản
5. Dễ mở rộng thêm replication hoặc dashboard mạnh hơn ở bản sau
