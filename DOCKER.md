# Forex — Deployment

📦 Barcha deploy fayllari **[`deploy/`](deploy/)** papkada:
- **[`deploy/docker/`](deploy/docker/)** — Docker Compose (2 fayl: minimal va to'liq)
- **[`deploy/kubernetes/`](deploy/kubernetes/)** — Kubernetes manifestlari (minikube / klaster)

Qo'llanmalar:
- **[deploy/docker/README.md](deploy/docker/README.md)** — Docker Compose deploy
- **[deploy/docker/HTTPS.md](deploy/docker/HTTPS.md)** — HTTPS (Traefik, Cloudflare domen)
- **[deploy/kubernetes/README.md](deploy/kubernetes/README.md)** — Kubernetes deploy

Tez boshlash (Docker):

```bash
cd deploy/docker
cp .env.example .env        # to'ldiring
docker compose up -d                                  # minimal
# yoki: docker compose -f docker-compose.full.yml up -d   # to'liq (HTTPS)
```
