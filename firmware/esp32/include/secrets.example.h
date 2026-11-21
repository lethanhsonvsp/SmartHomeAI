// Chép thành secrets.h (không commit) rồi điền.
#pragma once
#define WIFI_SSID "ten-wifi"
#define WIFI_PASS "mat-khau-wifi"
#define MQTT_HOST "192.168.1.10"  // IP LAN của máy chạy Mosquitto (docker compose), không phải localhost
#define MQTT_PORT 1883            // 1884 nếu dùng mosquitto-secure
#define MQTT_USER ""              // để trống với broker dev (anonymous)
#define MQTT_PASS ""
