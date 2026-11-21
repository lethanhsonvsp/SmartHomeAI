# Tapo P100 qua LAN

`test_p100.py` dùng `tapo` 0.13.0 trong `.venv`: `ApiClient(...).p100(ip)`, `get_device_info().device_on`, `on()` và `off()`. Từ 0.11.1 thư viện tự chọn TPAP khi thiết bị thông báo giao thức này. `python-kasa` 0.12.0 báo unsupported là dấu hiệu tổ hợp giao thức/firmware chưa được phiên bản đó hỗ trợ, không chứng minh thiết bị hỏng. Không cần thay đổi Python hệ thống hoặc cài lại thư viện.

Dùng email TP-Link ID và mật khẩu của tài khoản đã đăng ký ổ cắm trong ứng dụng Tapo. TPAP xác thực qua LAN bằng bằng chứng dựa trên thông tin này; không cần Camera Account (tài khoản riêng cho RTSP/ONVIF của camera). `TPAP_CREDENTIALS` cho biết thiết bị từ chối xác thực; kiểm tra tài khoản, mật khẩu và IP trước khi thử lại. Không suy luận chắc chắn từ lỗi này rằng thiết bị đang khóa. `TPAP_AUTH_ATTEMPTS_LIMIT` báo khóa tạm; chờ trước khi thử lại. `TPAP_HASH_MISMATCH` là lỗi xác minh thiết bị; kiểm tra IP và credentials. Không tự động thử lại. Nếu P100 được cấu hình theo phương thức đặc biệt, xác minh thông tin đăng nhập của thiết bị trong cấu hình thực tế.

Chạy ở thư mục gốc (mật khẩu được nhập ẩn):

```sh
.venv/bin/python test_p100.py
.venv/bin/python test_p100.py --control on
```

Chương trình đọc trạng thái trước; `--control on` hoặc `--control off` yêu cầu nhập chính xác `YES` trong terminal, rồi đọc lại trạng thái. Có thể dùng `--host` và `--timeout` (giây). Với tiến trình không tương tác, cung cấp `TAPO_USERNAME` và `TAPO_PASSWORD` qua môi trường được bảo vệ, không đưa mật khẩu vào lệnh hoặc file được commit. Kết quả JSON trên stdout; lỗi JSON đã lọc trên stderr, mã thoát khác 0. Không dùng `--control` không tương tác.

## Ranh giới tích hợp

`src/tapo_p100/service.py` chứa `TapoP100Service(host, username, password, timeout)`, là adapter async có `get_status()` trả `P100Status(on: bool)`, `turn_on()` và `turn_off()`. Import bằng `from src.tapo_p100 import TapoP100Service`. Giữ một instance trong một event loop; các thao tác được tuần tự hóa. Timeout là giới hạn cho từng thao tác, không phải toàn bộ tiến trình. Khi lệnh điều khiển timeout, trạng thái vật lý không chắc chắn: đọc lại trước khi quyết định gửi lệnh khác. Một phiên kết nối thất bại không tự đăng nhập lại; sau lỗi xác thực cần dừng worker, kiểm tra nguyên nhân và khởi động lại có chủ ý.

Adapter C# trong Hub dùng service này qua một tiến trình cục bộ: ánh xạ một P100 tới một ID `light` hoặc `fan`, nhận `home/device/{id}/set`, đọc trạng thái trước khi cho phép điều khiển, và publish trạng thái thực lên `home/device/{id}/state` theo schema hiện tại. Không publish trạng thái giả khi lỗi. Hub/Automation Engine đã dùng các topic này và Blazor gọi Hub; xem `sim/p100.py` làm ví dụ contract. Adapter dừng khi xác thực thất bại và chỉ bật khi cấu hình rõ ràng. CLI chỉ dành cho kiểm tra thủ công, không gọi `--control` từ Hub. Để tránh nhiều lần đăng nhập, không chạy nhiều worker hoặc CLI đồng thời cho cùng thiết bị.

Nguồn: [tapo changelog](https://docs.rs/crate/tapo/latest/source/CHANGELOG.md), [tapo API](https://github.com/mihai-dinculescu/tapo), [TP-Link Camera Account](https://www.tp-link.com/uk/support/faq/2790/?app=tapo).

## Dùng P100 thật với Hub C#

Hub có `TapoP100Adapter` bằng C#: nhận lệnh MQTT của Hub, đọc và công bố trạng thái thực. Phần TPAP chạy trong tiến trình Python cục bộ dùng thư viện `tapo`; Hub không tự thử lại khi phiên gặp lỗi. Một P100 tương ứng một ID, mặc định `light`. `fan` vẫn cần thiết bị riêng; một ổ cắm không thể điều khiển độc lập cả hai. Không chạy P100 giả hoặc một adapter khác cùng ID.

Khởi động Dashboard từ thư mục gốc sau khi cấp thông tin qua môi trường (lệnh `read -s` không hiện mật khẩu):

```bash
export TapoP100__Enabled=true
export TapoP100__DeviceId=light
export TapoP100__Host=192.168.0.8
read -r -p 'TP-Link ID: ' TAPO_USERNAME; export TAPO_USERNAME
read -r -s -p 'Mật khẩu: ' TAPO_PASSWORD; export TAPO_PASSWORD; printf '\n'
dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard
```

Dashboard chuyển biến môi trường cho Hub mới khởi động và khóa mô phỏng P100 của Dashboard khi bật chế độ thật. Nếu Hub đã chạy, phải dừng và khởi động lại Hub với cùng cấu hình. Khi Hub chạy riêng từ thư mục dự án, đặt các biến trên trước `dotnet run --project src/SmartHomeAI.Hub`; đồng thời tự dừng P100 giả. Cần `.venv` tại gốc có `tapo` 0.13.0. Cấu hình tùy chọn `TapoP100__Root`, `TapoP100__Python` và `TapoP100__TimeoutSeconds` khi chạy ngoài cây dự án. Để kích hoạt lại sau lỗi đăng nhập, kiểm tra nguyên nhân và chủ động khởi động lại; không tự động thử nhiều mật khẩu.

### Lưu đăng nhập để chạy lại

Chạy một lần để nhập email và mật khẩu ẩn:

```bash
tools/run-p100-dashboard.sh --setup
```

Sau đó mỗi lần chạy Dashboard với P100 thật:

```bash
tools/run-p100-dashboard.sh
```

Thông tin được lưu dạng văn bản trong `.tapo-p100-credentials` với quyền 600 và được Git bỏ qua. Tài khoản người dùng trên máy có quyền đọc tệp vẫn có thể đọc thông tin này; giữ an toàn tài khoản Ubuntu và các bản sao lưu. Chạy lại `--setup` để đổi thông tin. Cấu hình `TapoP100__DeviceId` và `TapoP100__Host` trong môi trường nếu không dùng mặc định. Nếu Hub đã chạy từ trước, hãy dừng Hub để Dashboard khởi động Hub mới nhận cấu hình.

### Đăng nhập ngay trên giao diện Blazor

Ô "Đèn / quạt trần" có form email TP-Link, mật khẩu, IP ổ cắm. Đăng nhập một lần: Dashboard gọi `POST /api/tapo/login` của Hub, Hub giữ phiên TPAP trong tiến trình cầu nối Python (mật khẩu chỉ nằm trong bộ nhớ, không ghi đĩa), sau đó nút Bật/Tắt của `light` điều khiển ổ cắm thật. "Đăng xuất" gọi `POST /api/tapo/logout`. Trạng thái ở `GET /api/tapo` (`idle|connecting|connected|error`). Lỗi đăng nhập không tự thử lại. Khởi động lại Hub thì phải đăng nhập lại. Đăng nhập tự tắt P100 giả của Dashboard; cần khởi động lại Hub cũ để có endpoint mới.
