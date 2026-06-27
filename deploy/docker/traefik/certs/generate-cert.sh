#!/usr/bin/env bash
# ============================================================
# Forex uchun self-signed (o'zi imzolagan) TLS sertifikat yaratish
# ============================================================
# IP manzil bilan HTTPS uchun ishlatiladi (domain shart emas).
# Serverda (Linux) shu papkada ishga tushiring:
#
#   chmod +x generate-cert.sh
#   ./generate-cert.sh 203.0.113.10        # <- o'z server IP'ingiz
#
# Natija: server.crt va server.key fayllari shu papkada hosil bo'ladi.
# Sertifikat 10 yil amal qiladi.

set -e

IP="${1:-}"
if [ -z "$IP" ]; then
  echo "Foydalanish: ./generate-cert.sh <SERVER_IP>"
  echo "Masalan:     ./generate-cert.sh 203.0.113.10"
  exit 1
fi

DIR="$(cd "$(dirname "$0")" && pwd)"

openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
  -keyout "$DIR/server.key" \
  -out "$DIR/server.crt" \
  -subj "/CN=$IP" \
  -addext "subjectAltName=IP:$IP"

chmod 600 "$DIR/server.key"

echo ""
echo "✅ Sertifikat yaratildi (IP=$IP, 10 yil):"
echo "   $DIR/server.crt"
echo "   $DIR/server.key"
echo ""
echo "Endi: docker compose -f docker-compose.yml -f docker-compose.traefik.yml up -d"
