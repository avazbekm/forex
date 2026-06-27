# Forex — domen bilan ishga tushirish (HTTPS)

Cloudflare domeningiz bilan, haqiqiy HTTPS sertifikati (Let's Encrypt) ostida. Hammasi
**bitta subdomen** ostida, yo'llar bilan:

| Manzil | Xizmat |
|--------|--------|
| `https://forex.example.com` | **API** (WPF ilova shuni ishlatadi) |
| `https://forex.example.com/pgadmin` | pgAdmin |
| `https://forex.example.com/forex-storage/...` | rasm fayllar (MinIO; yo'l = bucket nomi) |

> Misol uchun `forex.example.com` ishlatilgan — o'z domeningizni qo'ying. `SERVER_IP` = serveringiz IP,
> `username` = server foydalanuvchingiz. Fayllar yo'li `.env` dagi `MINIO_BUCKET_NAME` ga teng
> (default `forex-storage`).

---

## 1-qadam. Cloudflare (DNS + token)

**A) DNS yozuvi** — Cloudflare → `example.com` → DNS → Add record:

| Type | Name | Content | Proxy |
|------|------|---------|-------|
| A | `forex` | `SERVER_IP` | **DNS only** (kulrang bulut) |

(Faqat bitta yozuv — `forex.example.com`. Apex `example.com` dagi do'kon saytingizga tegmaydi.)

**B) API token** — Cloudflare → My Profile → API Tokens → Create Token →
**"Edit zone DNS"** shabloni → Zone = `example.com` → Create → tokenni nusxalang.

---

## 2-qadam. Serverga ulanib, papka oching

```bash
ssh username@SERVER_IP
mkdir forex && cd forex
```

(Agar serverda Docker bo'lmasa, bir marta o'rnating: `curl -fsSL https://get.docker.com | sh`)

---

## 3-qadam. Fayllarni serverga ko'chiring

O'z kompyuteringizda **yangi terminal** oching (Git Bash yoki PowerShell) va `deploy`
papkasidagi hamma narsani serverga yuboring:

```bash
scp -r "C:\Users\muqim\source\repos\forex\deploy\." username@SERVER_IP:~/forex/
```

---

## 4-qadam. Sozlamalarni to'ldiring (serverda, `~/forex` ichida)

```bash
cp .env.example .env
nano .env
```

`.env` da quyidagilarni to'ldiring:

```env
DOCKER_TAG=3.0.0                  # ARM server bo'lsa: arm64v3.0.0
POSTGRES_PASSWORD=kuchli-parol
PGADMIN_EMAIL=siz@email.com
PGADMIN_PASSWORD=kuchli-parol
JWT_SECRET_KEY=uzun-tasodifiy-kalit

# Domen (o'zingiznikini qo'ying):
DOMAIN=forex.example.com

# Cloudflare token (1-qadamdan):
CF_DNS_API_TOKEN=token-shu-yerga
```

Saqlang: `Ctrl+O`, `Enter`, `Ctrl+X`.

Yana ikki fayl:

```bash
cp servers.json.example servers.json                      # pgAdmin server ro'yxati
touch traefik/acme.json && chmod 600 traefik/acme.json    # sertifikat saqlanadigan fayl
```

---

## 5-qadam. Ishga tushiring

```bash
docker compose -f docker-compose.full.yml up -d
```

Bir-ikki daqiqada Traefik sertifikat oladi. Holatni ko'rish:

```bash
docker compose -f docker-compose.full.yml logs -f traefik
```

---

## 6-qadam. Tekshiring

Brauzerda oching (yashil qulf, ogohlantirishsiz):

- API hujjati: `https://forex.example.com/scalar/v1`
- pgAdmin: `https://forex.example.com/pgadmin`

**WPF ilovada** server manzilini `https://forex.example.com` qilib kiriting — tamom.
Rasmlar avtomatik `https://forex.example.com/forex-storage/...` orqali yuklanadi.

---

## Backup

Backup (zaxira agenti) `docker-compose.full.yml` ichida **allaqachon bor** — alohida fayl shart emas.
Faqat `.env` da backup qiymatlarini (`BACKUP_*`, `RCLONE_*`) to'ldiring va `cp rclone.conf.example rclone.conf`
qiling. Ishga tushganda backup avtomatik ishlaydi (standart: har kuni 02:00).

---

## Muammo bo'lsa

```bash
docker compose -f docker-compose.full.yml logs traefik
```

- Sertifikat olinmayapti → `.env` dagi `CF_DNS_API_TOKEN` va Cloudflare A yozuvini tekshiring.
- 502 / sahifa ochilmayapti → `docker ps` bilan konteynerlar ishlayotganini ko'ring.
- Rasm ko'rinmasa → `DOMAIN` va `MINIO_BUCKET_NAME` to'g'riligini, hamda rasm URL'i
  `https://forex.example.com/<bucket>/...` ekanini tekshiring.
