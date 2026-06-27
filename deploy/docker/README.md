# Forex — Deploy (Docker Compose)

Serverga o'rnatish uchun barcha fayllar shu papkada. Hamma buyruqlar **shu `deploy/docker/`
papka ichidan** ishga tushiriladi. (Kubernetes uchun: `../kubernetes/`.)

## 📂 Tuzilma — ikki compose fayl

```
deploy/docker/
├── docker-compose.yml        # MINIMAL: postgres + minio + app
├── docker-compose.full.yml   # TO'LIQ: + pgadmin + backup + traefik (HTTPS/domen)
├── .env / .env.example       # barcha sozlamalar (yagona fayl)
├── rclone.conf / .example    # backup tashqi manzili (bulut kalitlari)
├── servers.json / .example   # pgAdmin uchun tayyor server ro'yxati
├── HTTPS.md                  # HTTPS sozlash qo'llanmasi (Traefik, Cloudflare)
└── traefik/                  # reverse proxy + TLS konfiguratsiyasi
    ├── traefik.yml           #   statik config (Let's Encrypt + Cloudflare)
    └── dynamic/tls.yml       #   (ixtiyoriy IP/self-signed)
```

- **`docker-compose.yml`** — eng sodda: faqat postgres + minio + app. HTTPS yo'q.
- **`docker-compose.full.yml`** — hammasi bitta faylda: yuqoridagilar + pgAdmin + backup + Traefik
  (domen orqali haqiqiy HTTPS). Ishlab chiqarish (production) uchun shu.

## 🔖 Versiyalar (image teglari)

| Tag | Arxitektura | Foydalanuvchi |
|-----|-------------|---------------|
| `muqimjon/forex:3.0.0` | linux/amd64 | Oddiy serverlar (Intel/AMD) |
| `muqimjon/forex:arm64v3.0.0` | linux/arm64 | ARM serverlar |

`.env` da `DOCKER_TAG` orqali versiyani tanlaysiz. Eski versiyada qolish uchun o'sha
versiyaning `deploy/` papkasidan (git teg/release ichidan) foydalaning.

## 🚀 Tayyorgarlik (bir marta)

```bash
cd deploy/docker
cp .env.example .env                 # parol, domen, versiya — to'ldiring
```

To'liq (full) variant uchun qo'shimcha:
```bash
cp servers.json.example servers.json                      # pgAdmin server ro'yxati
cp rclone.conf.example rclone.conf                        # backup bulut kalitlari
touch traefik/acme.json && chmod 600 traefik/acme.json    # TLS sertifikat fayli
```

## ▶️ Ishga tushirish — ikki variant

| Variant | Tarkib | Buyruq |
|---------|--------|--------|
| **Minimal** | postgres + minio + app | `docker compose up -d` |
| **To'liq** | + pgadmin + backup + traefik (HTTPS) | `docker compose -f docker-compose.full.yml up -d` |

> To'liq variant uchun `.env` da `DOMAIN` va `CF_DNS_API_TOKEN` ham to'ldirilishi shart.
> SSH'dan boshlab to'liq sodda qadamlar: **[HTTPS.md](HTTPS.md)**.

## 🌐 Xizmatlar (URL)

| Xizmat | Minimal rejim | Traefik (domen) rejim |
|--------|---------------|------------------------|
| Backend API | http://SERVER_IP:5001 | **https://forex.example.com** |
| API hujjati | http://SERVER_IP:5001/scalar/v1 | https://forex.example.com/scalar/v1 |
| pgAdmin | http://SERVER_IP:8080 | **https://forex.example.com/pgadmin** |
| Rasm fayllari | http://SERVER_IP:9000 | https://forex.example.com/forex-storage/... |
| MinIO konsoli | http://SERVER_IP:9001 | chiqarilmaydi |
| PostgreSQL | SERVER_IP:5433 | SERVER_IP:5433 |

> Traefik (domen) rejimida to'g'ridan-to'g'ri portlar (5001/8080/9000/9001) yopiladi — hammasi
> bitta domen `forex.example.com` ostida (yo'llar bilan). WPF API manzili = `https://forex.example.com`.

## 💾 Backup (Zaxira agenti)

`backup` xizmati (`muqimjon/zaxira`) PostgreSQL bazasi **va** MinIO bucket'ini birga
zaxiralaydi, shifrlaydi va rclone orqali tashqi joyga yuklaydi (standart: har kuni 02:00).

Sozlash: `.env` dagi "Backup" bo'limi (`PROJECT_NAME`, `TZ`, `BACKUP_SCHEDULE`,
`BACKUP_PASSWORD`, `RCLONE_REMOTE`, `RCLONE_PATH`) + `rclone.conf`.

```bash
docker logs -f forex_backup     # jadval va zaxira loglari
ls docker-data/backup/          # lokal zaxira fayllari
```

## 🔒 HTTPS (domen)

Cloudflare domeningiz bilan haqiqiy (ogohlantirishsiz) Let's Encrypt sertifikati — har xizmat
bitta domen ostida (yo'llar bilan). SSH'dan boshlab to'liq sodda qadamlar: **[HTTPS.md](HTTPS.md)**.

## 🛠️ Boshqaruv

> To'liq variant ishlatsangiz, har buyruqqa `-f docker-compose.full.yml` qo'shing
> (yoki `export COMPOSE_FILE=docker-compose.full.yml` qiling).

```bash
docker compose logs -f app        # backend loglari
docker compose restart app        # faqat backendni qayta ishga tushirish
docker compose pull && docker compose up -d   # yangi versiyaga yangilash
docker compose down               # to'xtatish
```

## 📦 Qo'lda backup / restore

```bash
# Baza (qo'lda)
docker exec forex_postgres pg_dump -U postgres forex > backup.sql
cat backup.sql | docker exec -i forex_postgres psql -U postgres forex

# MinIO data
tar -czf minio-backup.tar.gz docker-data/minio/
```

## ⚠️ PostgreSQL 18 eslatmasi

Ushbu versiya `postgres:18.1-trixie` ishlatadi, volume yo'li `/var/lib/postgresql`
(avval PG16 da `/var/lib/postgresql/data` edi). PG16 dagi eski deploy'ni yangilashda
avval `pg_dump` bilan zaxira oling, so'ng yangi volume'ga tiklang.
