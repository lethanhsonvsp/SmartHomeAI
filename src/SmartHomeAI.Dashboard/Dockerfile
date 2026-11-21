# Build từ gốc repo: docker build -f src/SmartHomeAI.Dashboard/Dockerfile .
# Một ảnh = Dashboard + AI camera + mô phỏng: Dashboard bật/tắt AI và sim như tiến trình con, giống khi chạy trên máy.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/SmartHomeAI.Dashboard src/SmartHomeAI.Dashboard
RUN dotnet publish src/SmartHomeAI.Dashboard/SmartHomeAI.Dashboard -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# procps: Dashboard dừng tiến trình con bằng lệnh kill.
RUN apt-get update && apt-get install -y --no-install-recommends python3 python3-venv procps \
    && rm -rf /var/lib/apt/lists/* && python3 -m venv /opt/venv \
    && /opt/venv/bin/pip install --no-cache-dir numpy "opencv-python-headless>=4.8,<5" "onnxruntime>=1.18" "paho-mqtt>=2.0" "jsonschema>=4.0"
WORKDIR /repo
COPY ai/tools/fetch_model.py ai/tools/
# Mô hình tải lúc build và kiểm sha256; thêm bản khác: --build-arg AI_MODELS="yolov8m-pose yolov8s-pose"
ARG AI_MODELS="yolov8m-pose"
RUN /opt/venv/bin/python ai/tools/fetch_model.py $AI_MODELS
COPY docs/schemas docs/schemas
COPY sim sim
COPY ai/run.sh ai/
COPY ai/smarthome_ai ai/smarthome_ai
COPY ai/config ai/config
# ai/run.sh gọi ai/.venv/bin/python
RUN ln -s /opt/venv ai/.venv
COPY --from=build /out app
ENV ASPNETCORE_URLS=http://+:5037 Repo__Root=/repo Sim__Python=/opt/venv/bin/python
EXPOSE 5037 8090
WORKDIR /repo/app
ENTRYPOINT ["dotnet", "SmartHomeAI.Dashboard.dll"]
