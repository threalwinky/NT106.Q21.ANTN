# Netrix 1.0.4 Architecture

## Tổng quan

Netrix là hệ thống điều khiển máy tính từ xa theo mô hình client-server. Phía client là ứng dụng C# WinForms chạy trên Windows. Cùng một app có thể đóng vai trò host, controller hoặc viewer tùy người dùng tạo phòng hay tham gia phòng.

Phía server được tách thành ba phần chính: `auth-server` để xử lý tài khoản và JWT, `load-balancer` để chọn main server phù hợp, và cụm `main-server` để giữ room realtime, relay màn hình, input, chat và file transfer.

Cách tách này giúp phần đăng nhập không bị trộn với phần stream realtime. Khi cần scale, có thể chạy nhiều main server từ cùng một codebase, còn load balancer sẽ chọn node ít tải hoặc node đang giữ room.

Netrix hỗ trợ cả Internet mode và LAN mode. Dù chạy mode nào, hệ thống vẫn không cho kết nối ẩn danh: user phải đăng nhập, room phải có password, và controller phải được host duyệt trước khi điều khiển.

---

## Stream màn hình ở bản 1.0.4

Ở bản `1.0.4`, phần stream màn hình không còn chỉ dựa vào JPEG từng frame nữa. Host vẫn gửi theo nhịp khoảng `30 FPS`, nhưng đường chính bây giờ là encode H.264 bằng OpenH264 thông qua package `H264Sharp`.

Host chụp màn hình bằng DXGI Desktop Duplication nếu máy hỗ trợ, nếu không thì quay về GDI. Bitmap sau khi chụp được resize về tối đa `1280px`, ép kích thước chẵn để hợp với encoder, rồi đưa vào encoder H.264 với target `30 FPS` và bitrate khoảng `5 Mbps`.

Frame vẫn được mã hóa bằng room password. Binary packet giờ có thêm `codec`, `width`, `height`, `sent_at`, rồi mới tới payload đã encode. Nếu H.264 chạy được thì payload là H.264 bytes; nếu thiếu native DLL hoặc encoder lỗi, client tự quay về JPEG quality `45` để room vẫn dùng được.

Với H.264, client không cần tự so từng JPEG nữa vì encoder đã xử lý P-frame/keyframe. Host ép keyframe định kỳ khoảng 2 giây để peer mới join hoặc decoder bị lệch có thể bắt lại hình. Với fallback JPEG, client vẫn bỏ qua frame trùng để tiết kiệm băng thông.

Main server không decode H.264 và cũng không đọc nội dung màn hình. Server chỉ kiểm tra người gửi có role `host` hay không, sau đó relay binary packet cho peer còn lại trong room.

---

## Sơ đồ logic

```text
                 +----------------------+
                 |     Auth Server      |
                 | register / login     |
                 | bcrypt + JWT         |
                 | PostgreSQL sessions  |
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
  +----------------------+          +------------------------------+
  |  C# Client Host      |          | C# Client Remote Peer        |
  |  Windows / WinForms  |          | Windows / WinForms           |
  |                      |          | Role: Controller or Viewer   |
  +----------------------+          +------------------------------+
```

---

## Triển khai bằng Docker Compose

Toàn bộ phần server có thể chạy bằng `docker-compose.yml` trong thư mục `Netrix/1.0.4`.

```bash
docker-compose up -d --build
docker-compose ps
```

Stack gồm PostgreSQL, auth server, load balancer và ba main server:

| Service | Container | Chức năng | Port trên máy host |
|---|---|---|---|
| `cloud` | `netrix-cloud` | PostgreSQL lưu user/session | `5433` |
| `auth-server` | `netrix-auth-server` | Đăng ký, đăng nhập, cấp JWT | `8001` |
| `load-balancer` | `netrix-load-balancer` | Chọn main server | `8002` |
| `main-server-1` | `netrix-main-server-1` | Realtime node 1 | `8000` |
| `main-server-2` | `netrix-main-server-2` | Realtime node 2 | `8003` |
| `main-server-3` | `netrix-main-server-3` | Realtime node 3 | `8004` |

Docker healthcheck được dùng để biết service nào đã sẵn sàng. PostgreSQL dùng `pg_isready`, còn các FastAPI server dùng endpoint `/health`.

Trong compose, biến `NETRIX_ACCESS_LOG=false` được bật mặc định để log không bị đầy bởi request healthcheck. Nếu cần debug request chi tiết, có thể đổi biến này thành `true`.

Nếu dùng `docker-compose` v1 và thấy lỗi `KeyError: 'id'` khi follow log, đó là lỗi của compose CLI cũ khi đọc Docker event stream. Server không bị crash. Có thể kiểm tra bằng:

```bash
docker-compose ps
docker logs -f netrix-auth-server
docker logs -f netrix-load-balancer
docker logs -f netrix-main-server-1
docker logs -f netrix-main-server-2
docker logs -f netrix-main-server-3
```

---

## Cloudflare Tunnel

Khi chạy Internet mode, các service local được đưa ra public bằng Cloudflare Tunnel.

| Public hostname | Trỏ về local | Dùng cho |
|---|---|---|
| `auth.threalwinky.id.vn` | `http://localhost:8001` | Auth API |
| `load.threalwinky.id.vn` | `http://localhost:8002` | Load balancer API |
| `main.threalwinky.id.vn` | `http://localhost:8000` | Main server 1 |
| `main2.threalwinky.id.vn` | `http://localhost:8003` | Main server 2 |
| `main3.threalwinky.id.vn` | `http://localhost:8004` | Main server 3 |

Client C# mặc định gọi auth qua `https://auth.threalwinky.id.vn` và gọi load balancer qua `https://load.threalwinky.id.vn`. Sau khi user đăng nhập, client hỏi load balancer để lấy WebSocket URL của main server, ví dụ `wss://main.threalwinky.id.vn/ws`.

Ba main server trả về public WebSocket URL qua `/health`:

| Node | Public WebSocket URL |
|---|---|
| `main-server-1` | `wss://main.threalwinky.id.vn/ws` |
| `main-server-2` | `wss://main2.threalwinky.id.vn/ws` |
| `main-server-3` | `wss://main3.threalwinky.id.vn/ws` |

PostgreSQL không cần public cho client. Nếu có route `cloud.threalwinky.id.vn -> tcp://localhost:5433`, route đó chỉ phục vụ debug hoặc quản trị. Khi triển khai thật, database nên để private; `auth-server` đã truy cập được PostgreSQL qua Docker network bằng địa chỉ `cloud:5433`.

---

## Internet mode

Ở Internet mode, client không tự chọn main server. Sau khi đăng nhập thành công, client gọi load balancer. Load balancer kiểm tra health của các main server, chọn node phù hợp rồi trả về `ws_url`.

```text
C# Client
  |
  +--> Auth Server: register/login
  |      |
  |      +--> trả JWT
  |
  +--> Load Balancer: select-server
         |
         +--> kiểm tra /health của main-server-1/2/3
         +--> trả ws_url
  |
  v
Selected Main Server
  |
  +--> create/join room
  +--> relay frame/input/chat/file
```

Với Cloudflare Tunnel, URL client nhận được sẽ là dạng `wss://main.../ws`, nên dữ liệu realtime đi qua TLS.

---

## LAN mode

LAN mode dùng cho trường hợp hai máy ở cùng mạng nội bộ. User vẫn phải đăng nhập trước, nhưng client không hỏi load balancer để chọn node. Thay vào đó, user nhập thẳng WebSocket URL của main server trong ô `LAN Main Server`.

Ví dụ server chạy Docker trên máy có IP LAN `192.168.1.50`, client sẽ nhập:

```text
ws://192.168.1.50:8000/ws
```

Có thể dùng node khác nếu muốn:

```text
ws://192.168.1.50:8003/ws
ws://192.168.1.50:8004/ws
```

LAN mode vẫn dùng `room_id + room_password`, vẫn gửi JWT trong `create_room` / `join_room`, và vẫn kiểm tra LAN scope để tránh join nhầm từ mạng khác. Nếu client ở máy khác không kết nối được, thường là do firewall chưa mở port `8000`, `8003` hoặc `8004`.

---

## Client

Client nằm trong thư mục `client/client`. Đây là app WinForms duy nhất của Netrix, không tách project riêng cho host hay controller.

Khi user tạo room, client trở thành host: app capture màn hình, encode H.264 nếu được, mã hóa frame rồi gửi lên main server. Khi user join room, client có thể là controller hoặc viewer. Controller được gửi chuột/bàn phím sau khi host approve, còn viewer chỉ xem màn hình.

Client cũng quản lý chat, file transfer, theme, trạng thái room, danh sách participant và phần render màn hình remote.

Ở Internet mode, client dùng endpoint mặc định trong `NetrixEndpoints`:

```text
Auth Server   = https://auth.threalwinky.id.vn
Load Balancer = https://load.threalwinky.id.vn
```

Ở LAN mode, client dùng URL do user nhập trong ô `LAN Main Server`.

---

## Auth server

`auth-server` xử lý đăng ký, đăng nhập và kiểm tra JWT. Server này không xử lý room, frame hay input realtime.

Các endpoint chính:

| Endpoint | Chức năng |
|---|---|
| `POST /register` | Tạo user mới, hash password bằng `bcrypt`, trả JWT |
| `POST /login` | Kiểm tra password, trả JWT |
| `GET /validate` | Kiểm tra JWT và session |
| `GET /health` | Kiểm tra auth server và PostgreSQL |

Auth server lưu user và session trong PostgreSQL. Khi chạy bằng Docker Compose, database nằm ở service `cloud`, port nội bộ `5433`. Khi chạy local ngoài Docker, có thể dùng `localhost:5433`.

---

## Load balancer

`load-balancer` không proxy WebSocket. Nó chỉ chọn main server rồi trả URL về cho client.

Khi client gọi `/select-server`, load balancer sẽ đọc registry, gọi `/health` của từng main server, loại node down hoặc node trả về `ws_url` không hợp lệ, rồi chọn node ít tải hơn dựa trên số connection và số room.

Khi chạy ngoài Docker, registry mặc định là `servers.json`:

| Node | Health URL |
|---|---|
| `main-server-1` | `http://127.0.0.1:8000/health` |
| `main-server-2` | `http://127.0.0.1:8003/health` |
| `main-server-3` | `http://127.0.0.1:8004/health` |

Khi chạy Docker Compose, load balancer dùng `servers.compose.json` thông qua biến `NETRIX_LB_REGISTRY_PATH`:

| Node | Health URL trong Docker network |
|---|---|
| `main-server-1` | `http://main-server-1:8000/health` |
| `main-server-2` | `http://main-server-2:8003/health` |
| `main-server-3` | `http://main-server-3:8004/health` |

Nếu client truyền `room_id`, load balancer sẽ ưu tiên node đang giữ room đó. Cách này giúp reconnect vào đúng server khi room vẫn còn sống.

---

## Main server

`main-server` là phần realtime của hệ thống. Nó giữ room trong memory, validate join/create room, relay frame, input, chat và file transfer.

Mỗi main server chạy độc lập. Cùng một codebase có thể chạy thành ba node khác nhau:

```bash
python3 app.py --port 8000 --server-name main-server-1 --public-ws-url wss://main.threalwinky.id.vn/ws
python3 app.py --port 8003 --server-name main-server-2 --public-ws-url wss://main2.threalwinky.id.vn/ws
python3 app.py --port 8004 --server-name main-server-3 --public-ws-url wss://main3.threalwinky.id.vn/ws
```

Khi host tạo room, main server sinh room ID base32 dài 12 ký tự và lưu password hash. Khi peer join room, server kiểm tra room tồn tại, password đúng, mode khớp, LAN scope hợp lệ, và room chưa vượt giới hạn participant hiện tại.

Frame màn hình đi qua WebSocket binary packet. Main server không giải mã frame, chỉ kiểm tra người gửi là host rồi relay cho peer còn lại. Input thì đi chiều ngược lại: controller gửi input, main server kiểm tra quyền điều khiển rồi chuyển tiếp về host.

Các module chính:

| Module | Nội dung |
|---|---|
| `netrix_main/app_factory.py` | Tạo FastAPI app, state và service |
| `netrix_main/routes.py` | HTTP/WebSocket route, parse message, gọi service |
| `netrix_main/service.py` | Logic room, participant, broadcast, approve controller |
| `netrix_main/models.py` | Dataclass `Room` và `Participant` |
| `netrix_main/store.py` | Runtime state trong memory |
| `netrix_main/bootstrap.py` | Chuẩn bị import shared package |

---

## Shared package

`shared/netrix_shared` chứa phần dùng chung cho các server Python: đọc JWT config, hash password, verify password, tạo access token và decode token.

Tách phần này ra giúp `auth-server`, `main-server` và `load-balancer` dùng cùng logic bảo mật, tránh mỗi service tự viết một kiểu.

---

## Flow tạo và join room

Khi tạo room, user đăng nhập trước, chọn mode, rồi bấm create. Nếu là Internet mode, client hỏi load balancer để lấy `ws_url`. Nếu là LAN mode, client dùng URL do user nhập, ví dụ `ws://192.168.1.50:8000/ws`.

Sau khi kết nối WebSocket, client gửi message `create_room`. Main server validate JWT, tạo room ID, lưu password hash, gán client hiện tại làm host và bắt đầu nhận frame từ host.

Khi join room, user nhập room ID và room password. Main server kiểm tra room có tồn tại không, password có đúng không, mode có khớp không, LAN scope có hợp lệ không và room có còn slot cho peer hay không.

Nếu người join chọn role controller, host sẽ nhận request approve. Chỉ khi host duyệt thì controller mới gửi được input thật.

---

## Flow stream màn hình

Host capture màn hình, resize nếu cần, rồi ưu tiên encode bằng H.264/OpenH264. Nếu encoder H.264 không sẵn sàng thì host dùng lại JPEG fallback. Payload này được mã hóa AES-GCM bằng key sinh từ room password.

Packet gửi qua WebSocket có magic header `NXF1`, sau đó là nonce và ciphertext. Sau khi giải mã, peer đọc `codec`, `width`, `height`, `sent_at` rồi render theo đúng codec: H.264 thì đưa qua decoder OpenH264, JPEG thì render bằng `Image.FromStream`.

Vì frame đi qua binary WebSocket nên không cần base64 ở đường chính. Main server chỉ relay packet đã mã hóa, không decode video.

---

## Flow remote input

Controller phải click vào vùng màn hình remote để focus trước khi gửi input. Client gửi event chuột hoặc bàn phím lên main server. Server kiểm tra client có role controller và đã được host approve chưa.

Nếu hợp lệ, input được gửi về host. Host nhận event và áp dụng lên máy local bằng API input của Windows.

Viewer không được gửi input. Nếu host deny controller request, peer sẽ bị chuyển về viewer mode.

---

## Bảo mật

Netrix dùng nhiều lớp bảo vệ thay vì chỉ dựa vào một password phòng.

User phải đăng nhập để lấy JWT. Khi tạo hoặc join room, client gửi token để main server validate. Room luôn có password, và password này cũng được dùng để tạo key mã hóa payload trong room.

LAN và Internet mode không join chéo. LAN room còn bị khóa theo `network_scope`, giúp hạn chế việc một client ngoài mạng LAN cố join vào room LAN.

Controller không có quyền điều khiển ngay khi join. Host phải approve request trước. Chat, file và input đi qua `secure_payload`; frame màn hình đi qua encrypted binary packet. Khi chạy Internet mode, transport bên ngoài là TLS/WSS qua Cloudflare Tunnel.

---

## Ghi chú chạy LAN

LAN mode thường dùng khi host và peer cùng mạng nội bộ.

Trên máy chạy server:

```bash
docker-compose up -d --build
```

Sau đó lấy IP LAN của máy server, ví dụ `192.168.1.50`. Trên client, đăng nhập trước, chọn mode `LAN`, rồi nhập:

```text
ws://192.168.1.50:8000/ws
```

Host tạo room. Peer dùng cùng URL, room ID và room password để join.

Nếu peer ở máy khác không vào được, kiểm tra firewall của máy server. Các port cần mở cho main server là `8000`, `8003`, `8004`. PostgreSQL port `5433` không cần mở cho Netrix client.

---

## Failover

Failover hiện tại nằm ở bước chọn node. Nếu một main server không trả lời `/health`, load balancer sẽ loại node đó khỏi danh sách và chọn node khác cho request tiếp theo.

Điều này giúp hệ thống vẫn tạo room mới được khi một node chết. Tuy nhiên room state hiện vẫn nằm trong memory của từng main server. Nếu node đang giữ room bị tắt, room đó cũng mất theo. Bản hiện tại chưa có shared state hoặc replication giữa các main server.

---

## Giới hạn hiện tại

Bản `1.0.4` vẫn là MVP theo hướng rõ kiến trúc trước. Một room hiện chỉ hỗ trợ `1 host + 1 remote peer`. Room state vẫn lưu trong memory, nên chưa có session migration giữa các node. Client trong repo hiện là C# WinForms dành cho Windows.

Các hướng mở rộng hợp lý cho bản sau là lưu room state vào Redis/PostgreSQL, thêm dashboard server, hỗ trợ nhiều viewer/controller cùng lúc, và bổ sung cơ chế reconnect tốt hơn khi main server bị restart. Riêng phần video, bước tiếp theo có thể là adaptive bitrate/resolution theo mạng thật.

---

## Kết luận

Netrix `1.0.4` đã có kiến trúc đủ rõ để chạy và trình bày: auth tách riêng, load balancer chọn node, main server xử lý realtime, client xử lý UI/capture/render/input, còn shared package giữ phần security dùng chung.

Điểm quan trọng của bản này là hệ thống chạy được cả LAN và Internet, có Docker Compose cho toàn bộ server stack, có Cloudflare Tunnel cho public endpoint, và phần stream màn hình đã chuyển sang hướng video codec thật thay vì JPEG-only.
