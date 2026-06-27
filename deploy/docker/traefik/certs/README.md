# traefik/certs

Bu papkada TLS sertifikatlari turadi. **Sertifikat fayllari git'ga commit qilinmaydi**
(`.gitignore`da: `*.crt`, `*.key`, `*.pem`).

Sertifikat yaratish uchun:

```bash
# Linux server (tavsiya etiladi)
./generate-cert.sh <SERVER_IP>

# Windows (test uchun)
.\generate-cert.ps1 -Ip <SERVER_IP>
```

Natijada `server.crt` va `server.key` hosil bo'ladi — Traefik shularni ishlatadi.

To'liq yo'riqnoma: [`HTTPS.md`](../../HTTPS.md)
