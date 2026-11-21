# Nhật ký tích hợp camera Tapo C222 (16/11/2026)

Ghi lại quá trình đưa camera thật vào hệ thống, các lỗi gặp phải, cách xử lý, số đo và những giới hạn còn lại.

## 1. Kết nối camera

- Camera: Tapo C222, nối wifi, IP `192.168.0.5`, luồng RTSP `stream2` (luồng phụ 640x360).
- Kiểm tra trước bằng ffplay:
  `ffplay -rtsp_transport tcp ... "rtsp://<user>:<mật khẩu>@192.168.0.5:554/stream2"`.
  Mật khẩu có ký tự `@` phải viết thành `%40` trong URL.
- Tích hợp: AI nhận nguồn qua `--source`. Dashboard lấy giá trị này từ `Ai:Source` trong
  `src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard/appsettings.json`, nên chỉ cần đổi
  `Ai:Source` từ `"0"` (webcam) sang URL RTSP. Không phải sửa code.
- Chạy: `dotnet run --project src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard`, mở `http://localhost:5037`.

Lưu ý bảo mật: URL camera có mật khẩu đặt trong `appsettings.Local.json` (chạy `dotnet run`) hoặc `AI_SOURCE` trong `.env` (Docker); cả hai không commit.
Nên chuyển sang `dotnet user-secrets` hoặc biến môi trường `Ai__Source`.

## 2. Lỗi nhận tư thế: nằm mà báo đứng/ngồi

### Hiện tượng
Người nằm trên đệm (vùng `sofa`) bị nhận là `sitting`, có lúc là `standing`. Hậu quả: quạt không bật,
nhật ký ghi "người ở sofa nhưng chưa nằm".

### Nguyên nhân
Camera gắn ở góc trần, soi chéo xuống. Quy tắc trong `ai/smarthome_ai/posture.py` được viết cho camera
nhìn ngang:
- nằm khi trục vai→hông gần nằm ngang trên ảnh, hoặc khi hộp rộng hơn cao;
- ngồi/đứng dựa vào khoảng hông–gối theo chiều dọc ảnh.

Nhìn từ trên cao, người nằm dọc theo chiều cao của ảnh (đầu ở trên, chân ở dưới) cho hình 2D giống hệt
người đứng hoặc ngồi. Chỉnh ngưỡng cũng không tách được.

### Lần sửa thứ nhất (sai, đã hoàn tác)
Đã thử cho tỉ lệ hộp (rộng/cao > 1.1 thì là nằm) được ưu tiên hơn keypoint. Đo lại trên ảnh thật thì tỉ lệ
hộp chỉ khoảng 0.8, vì lần ước lượng đầu đã nhân hệ số khung hình hai lần. Bản sửa không có tác dụng mà
còn làm người ngồi duỗi chân dễ bị nhận nhầm là nằm, nên đã hoàn tác.

### Cách xử lý: luật theo vùng, bật/tắt trong phần setup camera
Không dời được camera, nên thêm tùy chọn **"Cam trên cao: ở sofa coi là nằm"**:
- Khi bật: người **không di chuyển** (`sitting` hoặc `standing`) có **tâm hộp** nằm trong vùng `sofa`
  được tính là `lying`. Người đang đi (`moving`) vẫn giữ nguyên.
- Phải xét tâm hộp chứ không xét điểm chân: lúc nằm, chân thường thò ra ngoài vùng sofa, mà tư thế
  `standing` lại lấy vùng theo chân, nên trước đây bị xếp vào phòng khách.
- Bật/tắt bằng nút trong hàng lớp phủ của Dashboard (cùng hàng "Khung xương/Hộp/Nhãn/Vùng"), có hiệu
  lực ngay. Giá trị lúc khởi động lấy từ `Ai:SofaLying` (đang để `true`). Đổi bằng nút chỉ giữ tới khi AI
  khởi động lại.
- Code: `ai/smarthome_ai/pipeline.py` (luật), `stream.py` (khóa `sofa_lying` trong `/config`),
  `__main__.py` (`--sofa-lying`), `AiRunner.cs`, `Home.razor`. Test: `test_sofa_lying_maps_sitting_in_sofa`.

Đánh đổi: khi bật, người ngồi thật trên sofa, hoặc đứng sát đệm mà tâm hộp lọt vào vùng, cũng bị tính là
nằm, nên quạt có thể bật. Nên vẽ vùng sofa ôm sát đệm.

Kết quả kiểm tra trên camera thật: người trên đệm hiện `lying [sofa]`, bảng "Đang thấy" ghi "Sofa – Nằm".

## 3. Kết nối lại khi mất camera

Đã có sẵn: URL `rtsp://` được coi là luồng trực tiếp. Khi đọc khung thất bại, AI phát `camera_ok=false`,
chờ 2 giây rồi mở lại luồng, lặp tới khi có hình (`pipeline.py`, `source.py`).

Điểm yếu chưa sửa:
- OpenCV không đặt thời hạn chờ, nên khi rớt wifi có thể treo khoảng 30 giây mới phát hiện;
- OpenCV mặc định nhận RTSP qua UDP, dễ vỡ hình qua wifi (ffplay chạy ổn với TCP);
- IP camera có thể đổi nếu router cấp động. Nên đặt IP cố định (DHCP reservation) cho camera.

## 4. Đổi mô hình nhận người

### Hiện tượng
Có lúc người nằm rõ ràng mà hệ thống báo "Không thấy ai". Mô hình cũ là YOLOv8n-pose (bản nhỏ nhất),
ngưỡng tin cậy 0.45. Người nhìn từ trên cao chỉ được chấm 0.3–0.5 nên hay bị ngưỡng này loại.

### Đo trên camera thật
Máy: Intel i7-10610U, 8 luồng, chạy onnxruntime CPU, ảnh vào 640. Lấy 40 khung, mỗi khung cách nhau 0,5 giây,
người ngồi xếp bằng trên đệm (có người trong cả 40 khung).

| Mô hình | Thời gian / khung | Ngưỡng 0.45 | Ngưỡng 0.3 | Tin cậy trung bình |
|---|---|---|---|---|
| YOLOv8n-pose | ~75 ms | 20/40 | 40/40 | 0.47 |
| YOLOv8s-pose | ~170 ms | 34/40 | 37/40 | 0.54 |

Nhận xét:
- Hạ ngưỡng xuống 0.3 là thay đổi có lợi nhất: bản n tăng từ 20/40 lên 40/40 khung.
- Bản s chắc hơn ở ngưỡng cũ và có độ tin cậy cao hơn, nên khớp xương ổn định hơn. Đổi lại nó chậm gấp
  khoảng 2,3 lần, nhưng vẫn kịp mức 4 khung/giây (250 ms mỗi khung).
- Mới đo một cảnh (ngồi), chưa đo cảnh nằm và chưa đếm nhận nhầm, nên chưa đủ để kết luận bản nào tốt hơn.

### Thay đổi
- Mặc định dùng `models/yolov8s-pose.onnx`, ngưỡng `conf=0.3` (`ai/smarthome_ai/pose.py`).
- Đổi mô hình bằng `--model <tệp.onnx>`, hoặc `Ai:Model` trong `appsettings.json`
  (ví dụ `"ai/models/yolov8n-pose.onnx"`; để trống thì dùng bản s).
- `ai/tools/fetch_model.py` tải cả hai bản từ Hugging Face (Xenova, bản xuất ONNX của Ultralytics) và kiểm
  sha256. Hai bản cùng định dạng đầu ra `[1, 56, 8400]` nên không phải sửa phần giải mã.
- Giấy phép vẫn là AGPL-3.0 (Ultralytics), như bản cũ.

### Đo thêm bản m và l, chọn mặc định
Đo lần hai, cùng cách (40 khung, người cúi/ngồi trên đệm, lúc đo AI trên Dashboard vẫn chạy song song nên
thời gian có thể cao hơn khi chạy riêng). Bản x không có trên nguồn Xenova nên không thử.

| Mô hình | Thời gian / khung | Ngưỡng 0.3 | Ngưỡng 0.45 | Tin cậy trung bình |
|---|---|---|---|---|
| YOLOv8n-pose | ~72 ms | 35/40 | 19/40 | 0.45 |
| YOLOv8s-pose | ~170 ms | 40/40 | 23/40 | 0.46 |
| YOLOv8m-pose | ~406 ms | 40/40 | 33/40 | 0.62 |
| YOLOv8l-pose | ~780 ms | 40/40 | 39/40 | 0.73 |

Mô hình càng lớn càng chắc. Bản l chính xác nhất nhưng chỉ nhận được khoảng 1,3 lần/giây: tư thế và vùng
cập nhật chậm (cửa sổ bỏ phiếu 8 khung thành khoảng 6 giây). Bản m nhận khoảng 2,5 lần/giây, độ tin cậy
cao hơn hẳn s, nên **chọn m làm mặc định**.

Dashboard có ô chọn mô hình cạnh công tắc Camera, sắp theo thứ tự đề xuất: m, l, s, n. Chỉ hiện các bản
đã tải về. Đổi lựa chọn thì AI tự khởi động lại; lựa chọn lưu ở `ai/config/model.txt` (không commit).
Mặc định ban đầu lấy từ `Ai:Model`. Dashboard chỉ nhận tên nằm trong danh sách cố định ở `AiRunner.cs`,
trình duyệt không truyền được đường dẫn tùy ý.

## 5. Việc còn lại

1. Ép RTSP qua TCP và đặt thời hạn chờ mở/đọc khoảng 5 giây trong `CvSource._open`.
2. Đo thêm cảnh nằm và cảnh phòng trống, cho cả hai mô hình: đếm số khung bắt được và số lần nhận nhầm,
   rồi mới chốt mô hình và ngưỡng.
3. Nếu vẫn hay sai: quay clip từ chính camera này (đứng/ngồi/nằm), dán nhãn vài trăm khung rồi fine-tune
   mô hình, hoặc huấn luyện một bộ phân loại tư thế nhỏ trên keypoint.
4. Chuyển mật khẩu camera ra khỏi `appsettings.json`.
