// Nút cảm biến esp1 của SmartHomeAI. Hợp đồng MQTT: docs/mqtt-topics.md, docs/schemas/.
//   home/sensor/esp1/env         {ts,temp_c,humidity,lux}  mỗi 10 giây
//   home/sensor/esp1/door        {ts,open}                 retained, khi cửa đổi trạng thái
//   home/sensor/esp1/status      {ts,online}               retained, LWT báo offline
//   home/sensor/esp1/buzzer/set  {ts,on}                   Hub gửi xuống
//
// Nối dây (ESP32 DevKit V1 30 chân):
//   SHT30, BH1750: VCC -> 3V3, GND -> GND, SDA -> GPIO21, SCL -> GPIO22 (chung một bus I2C)
//   MC-38:         một dây -> GPIO27, dây kia -> GND (dùng điện trở kéo lên bên trong)
//   Buzzer 5V:     GPIO25 -> điện trở 1k -> cực B transistor NPN (S8050/2N2222); E -> GND;
//                  buzzer (+) -> VIN (5V), buzzer (-) -> cực C. Không nối buzzer 5V thẳng vào GPIO.
//                  Mô-đun buzzer 3 chân (có sẵn transistor): I/O -> GPIO25, thường kích mức thấp: đặt BUZZER_ON = LOW.
#include <Arduino.h>
#include <PubSubClient.h>
#include <WiFi.h>
#include <Wire.h>
#include <time.h>

#include "secrets.h"

#define NODE "esp1"
const int PIN_SDA = 21, PIN_SCL = 22, PIN_DOOR = 27, PIN_BUZZER = 25;
const int DOOR_OPEN = HIGH;  // MC-38 NC: cửa đóng (nam châm sát) thì mạch kín, chân bị kéo xuống LOW; đứt dây cũng báo mở
const int BUZZER_ON = HIGH;
const uint8_t SHT30_ADDR = 0x44, BH1750_ADDR = 0x23;  // SHT30 0x45 hoặc BH1750 0x5C nếu chân ADDR nối VCC
const uint32_t ENV_MS = 10000, DEBOUNCE_MS = 50, RETRY_MS = 5000;

const char *T_ENV = "home/sensor/" NODE "/env", *T_DOOR = "home/sensor/" NODE "/door",
           *T_STATUS = "home/sensor/" NODE "/status", *T_BUZZER = "home/sensor/" NODE "/buzzer/set";

WiFiClient net;
PubSubClient mqtt(net);

void buzzer(bool on) { digitalWrite(PIN_BUZZER, on ? BUZZER_ON : !BUZZER_ON); }

// Giờ UTC dạng ISO 8601 (schema bắt buộc ts); false khi NTP chưa đồng bộ.
bool isoNow(char *out, size_t n) {
  time_t t = time(nullptr);
  if (t < 1700000000) return false;
  struct tm tm;
  gmtime_r(&t, &tm);
  strftime(out, n, "%Y-%m-%dT%H:%M:%SZ", &tm);
  return true;
}

uint8_t crc8(const uint8_t *d) {  // CRC của SHT30: đa thức 0x31, khởi tạo 0xFF, trên 2 byte
  uint8_t c = 0xFF;
  for (int i = 0; i < 2; i++) {
    c ^= d[i];
    for (int b = 0; b < 8; b++) c = c & 0x80 ? (c << 1) ^ 0x31 : c << 1;
  }
  return c;
}

bool readSht30(float &tempC, float &hum) {
  Wire.beginTransmission(SHT30_ADDR);
  Wire.write(0x24); Wire.write(0x00);  // đo một lần, độ lặp cao
  if (Wire.endTransmission() != 0) return false;
  delay(20);
  uint8_t d[6];
  if (Wire.requestFrom(SHT30_ADDR, (uint8_t)6) != 6) return false;
  for (auto &b : d) b = Wire.read();
  if (crc8(d) != d[2] || crc8(d + 3) != d[5]) return false;
  tempC = -45 + 175 * ((d[0] << 8 | d[1]) / 65535.0f);
  hum = 100 * ((d[3] << 8 | d[4]) / 65535.0f);
  return true;
}

bool readBh1750(float &lux) {  // đang ở chế độ đo liên tục độ phân giải cao (đặt trong setup)
  if (Wire.requestFrom(BH1750_ADDR, (uint8_t)2) != 2) return false;
  uint16_t raw = Wire.read() << 8;
  raw |= Wire.read();
  lux = raw / 1.2f;
  return true;
}

void pubDoor(bool open) {
  char ts[24], msg[80];
  if (!isoNow(ts, sizeof ts)) return;
  snprintf(msg, sizeof msg, "{\"ts\":\"%s\",\"open\":%s}", ts, open ? "true" : "false");
  mqtt.publish(T_DOOR, msg, true);
  Serial.printf("door %s\n", msg);
}

void pubEnv() {
  char ts[24], msg[128];
  float t, h, lux;
  // Thiếu một cảm biến thì không gửi: Hub tự coi là mất dữ liệu, không nhận số giả.
  if (!readSht30(t, h)) { Serial.println("SHT30 không trả lời (kiểm tra dây, địa chỉ 0x44)"); return; }
  if (!readBh1750(lux)) { Serial.println("BH1750 không trả lời (kiểm tra dây, địa chỉ 0x23)"); return; }
  if (!isoNow(ts, sizeof ts)) return;
  snprintf(msg, sizeof msg, "{\"ts\":\"%s\",\"temp_c\":%.1f,\"humidity\":%.1f,\"lux\":%.0f}", ts, t, constrain(h, 0.0f, 100.0f), lux);
  mqtt.publish(T_ENV, msg);
  Serial.printf("env %s\n", msg);
}

void onMessage(char *topic, byte *payload, unsigned int len) {
  // shortcut: chỉ tìm "on":true/false trong chuỗi, không phân tích JSON đầy đủ; thêm ArduinoJson khi có lệnh nhiều trường hơn
  char buf[96];
  len = min(len, (unsigned int)sizeof buf - 1);
  memcpy(buf, payload, len);
  buf[len] = 0;
  const char *p = strstr(buf, "\"on\"");
  if (!p) return;
  p += 4;
  while (*p == ' ' || *p == ':') p++;
  if (strncmp(p, "true", 4) == 0) buzzer(true);
  else if (strncmp(p, "false", 5) == 0) buzzer(false);
  Serial.printf("buzzer/set %s\n", buf);
}

bool connectMqtt() {
  char ts[24], will[64];
  if (!isoNow(ts, sizeof ts)) return false;  // chưa có giờ thì chưa gửi được tin hợp lệ
  snprintf(will, sizeof will, "{\"ts\":\"%s\",\"online\":false}", ts);
  const char *user = strlen(MQTT_USER) ? MQTT_USER : nullptr;
  if (!mqtt.connect("smarthome-" NODE, user, user ? MQTT_PASS : nullptr, T_STATUS, 1, true, will)) {
    Serial.printf("MQTT lỗi, mã %d\n", mqtt.state());
    return false;
  }
  char msg[64];
  snprintf(msg, sizeof msg, "{\"ts\":\"%s\",\"online\":true}", ts);
  mqtt.publish(T_STATUS, msg, true);
  mqtt.subscribe(T_BUZZER, 1);
  Serial.println("MQTT đã nối");
  return true;
}

void setup() {
  Serial.begin(115200);
  pinMode(PIN_BUZZER, OUTPUT);
  buzzer(false);
  pinMode(PIN_DOOR, INPUT_PULLUP);
  Wire.begin(PIN_SDA, PIN_SCL);
  Wire.beginTransmission(BH1750_ADDR);
  Wire.write(0x10);  // đo liên tục, 1 lux, 120 ms/lần
  Wire.endTransmission();
  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  configTime(0, 0, "pool.ntp.org", "time.google.com");  // UTC; cần Internet hoặc đổi sang máy chủ NTP trong LAN
  mqtt.setServer(MQTT_HOST, MQTT_PORT);
  mqtt.setCallback(onMessage);
}

void loop() {
  static uint32_t lastTry = -RETRY_MS, lastEnv = -ENV_MS, doorAt = 0;
  static int doorRaw = -1, doorSent = -1;
  uint32_t now = millis();

  if (!mqtt.connected()) {
    buzzer(false);  // mất Hub thì không ai tắt còi được: tự tắt
    doorSent = -1;  // nối lại thì gửi lại trạng thái cửa
    if (WiFi.status() == WL_CONNECTED && now - lastTry >= RETRY_MS) {
      lastTry = now;
      connectMqtt();
    }
    return;
  }
  mqtt.loop();

  int d = digitalRead(PIN_DOOR);
  if (d != doorRaw) { doorRaw = d; doorAt = now; }
  if (doorRaw != doorSent && now - doorAt >= DEBOUNCE_MS) {
    doorSent = doorRaw;
    pubDoor(doorSent == DOOR_OPEN);
  }

  if (now - lastEnv >= ENV_MS) {
    lastEnv = now;
    pubEnv();
  }
}
