#!/bin/sh
# Sinh infra/mosquitto/passwd từ MQTT_PASSWORD_{HUB,AI,SENSOR,DEVICE} (đọc từ .env nếu có) và sao acl.example -> acl nếu chưa có.
set -e
cd "$(dirname "$0")"
[ -f ../../.env ] && { set -a; . ../../.env; set +a; }
: "${MQTT_PASSWORD_HUB:?} ${MQTT_PASSWORD_AI:?} ${MQTT_PASSWORD_SENSOR:?} ${MQTT_PASSWORD_DEVICE:?}"
[ -f acl ] || cp acl.example acl
: > passwd
for u in hub ai sensor device; do
  eval "p=\$MQTT_PASSWORD_$(echo $u | tr a-z A-Z)"
  docker run --rm -v "$PWD:/w" eclipse-mosquitto:2 mosquitto_passwd -b /w/passwd "$u" "$p"
done
# mosquitto trong container chạy uid 1883, cần đọc được
chmod 644 passwd acl
echo "đã tạo $PWD/passwd và acl"
