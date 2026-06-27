# Forex — Kubernetes

Docker Compose bilan bir xil ilova, lekin Kubernetes'da. Yo'naltirish ham aynan o'sha
(bitta domen + yo'llar):

```
https://DOMAIN                 -> app (API, WPF shuni ishlatadi)
https://DOMAIN/pgadmin         -> pgAdmin
https://DOMAIN/forex-storage   -> rasm fayllar (MinIO; yo'l = bucket nomi)
```

## 📂 Fayllar

| Fayl | Vazifa |
|------|--------|
| `namespace.yaml` | `forex` namespace |
| `configmap.yaml` | maxfiy bo'lmagan sozlamalar (DOMAIN, user, bucket...) |
| `secret.example.yaml` | maxfiylar namunasi → `secret.yaml` (git'ga kirmaydi) |
| `postgres.yaml` | PostgreSQL (PVC + Deployment + Service) |
| `minio.yaml` | MinIO (PVC + Deployment + Service) |
| `app.yaml` | Backend API (Deployment + Service) |
| `pgadmin.yaml` | pgAdmin (Deployment + Service) |
| `ingress.yaml` | Yo'naltirish + HTTPS (bitta domen, yo'llar) |
| `issuer.example.yaml` | cert-manager + Let's Encrypt (production HTTPS) |
| `backup.yaml` | Zaxira agenti (ixtiyoriy) |

## ❓ minikube'da HTTPS uchun Traefik kerakmi?

**Yo'q, Traefik shart emas.** Kubernetes'da reverse proxy o'rnini **Ingress Controller**
egallaydi. Docker'dagi Traefik bu yerda kerak emas — uning ishini Ingress bajaradi.

- **minikube**: ichida **nginx** ingress controller bor — `minikube addons enable ingress`
  bilan yoqasiz. HTTPS uchun shu yetadi (Traefik o'rnatish shart emas).
- **HTTPS sertifikati**:
  - **Production** (haqiqiy domen): `cert-manager` + Let's Encrypt (Cloudflare DNS-01) —
    sertifikatni avtomatik oladi va yangilaydi. Qaysi ingress (nginx yoki traefik) bo'lishidan qat'i nazar ishlaydi.
  - **minikube** (lokal, domen yo'q): Let's Encrypt ishlamaydi (ommaviy domen kerak) →
    self-signed sertifikat yoki oddiy HTTP bilan sinab ko'rasiz.
- Xohlasangiz Ingress controller sifatida **Traefik**ni ham o'rnatish mumkin (Helm bilan),
  lekin **majburiy emas** — nginx eng sodda yo'l.

---

## 🧪 minikube (lokal sinov)

```bash
minikube start
minikube addons enable ingress            # nginx ingress controller

# Sozlamalar
cp secret.example.yaml secret.yaml        # parollarni to'ldiring
# configmap.yaml va ingress.yaml da DOMAIN ni 'forex.local' qiling (lokal sinov uchun)

kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml -f secret.yaml
kubectl apply -f postgres.yaml -f minio.yaml -f app.yaml -f pgadmin.yaml -f ingress.yaml

# Domenni minikube IP ga bog'lash (hosts fayl):
echo "$(minikube ip)  forex.local" | sudo tee -a /etc/hosts
```

Ochish: `http://forex.local/scalar/v1` (lokalda HTTPS sertifikati self-signed bo'ladi —
brauzer ogohlantiradi, "Proceed" bosasiz; yoki `ingress.yaml` dagi `tls:` blokini olib http bilan sinaysiz).

---

## 🚀 Production (haqiqiy klaster + domen)

```bash
# 1) cert-manager (bir marta)
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml

# 2) Cloudflare token (DNS-01 challenge uchun)
kubectl -n cert-manager create secret generic cloudflare-token --from-literal=api-token=SIZNING_TOKEN
kubectl apply -f issuer.example.yaml

# 3) DOMAIN ni o'zgartiring: configmap.yaml (DOMAIN) va ingress.yaml (host + tls.hosts)
#    issuer.example.yaml da email'ni o'zgartiring.

# 4) Maxfiylar
cp secret.example.yaml secret.yaml        # to'ldiring
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml -f secret.yaml

# 5) Xizmatlar
kubectl apply -f postgres.yaml -f minio.yaml -f app.yaml -f pgadmin.yaml -f ingress.yaml

# 6) Cloudflare'da A yozuv: forex.DOMAIN -> ingress controller tashqi IP
kubectl -n forex get ingress           # ADDRESS ustunidagi IP
```

Bir-ikki daqiqada cert-manager sertifikat oladi. Ochish: `https://forex.DOMAIN`.
WPF ilovada server manzili = `https://forex.DOMAIN`.

Backup ham kerak bo'lsa:
```bash
kubectl -n forex create secret generic rclone-config --from-file=rclone.conf=./rclone.conf
kubectl apply -f backup.yaml
```

## ⚠️ Eslatmalar

- ARM klaster (mas. Apple Silicon minikube): `app.yaml` da image'ni `muqimjon/forex:arm64v3.0.0` qiling.
- `secret.yaml` va `rclone.conf` — git'ga kirmaydi (`.gitignore`). Faqat `*.example` saqlanadi.
- DOMAIN ikki joyda: `configmap.yaml` va `ingress.yaml` — ikkalasini bir xil qiling.
