# Forex
Business Management System

<img width="930" height="942" alt="image" src="https://github.com/user-attachments/assets/b180adf4-a103-4d67-a99c-6618477e7d08" />

## 🏗️ Architecture

**Clean Architecture** with following layers:
- **Domain**: Business entities and core logic
- **Application**: Use cases, DTOs, and interfaces
- **Infrastructure**: Data access, file storage (MinIO), external services
- **WebApi**: REST API endpoints
- **WPF**: Desktop client application

## 🚀 Quick Start

Deploy fayllari **`deploy/`** da: `docker/` (Docker Compose) va `kubernetes/` (k8s).

**Docker Compose** (`deploy/docker/`) — ikki variant:

```bash
cd deploy/docker
cp .env.example .env                 # parol, domen, versiya (DOCKER_TAG)

# Minimal (postgres + minio + app):
docker compose up -d

# To'liq (+ pgadmin + backup + traefik/HTTPS, bitta domen):
docker compose -f docker-compose.full.yml up -d
```

**Kubernetes** (`deploy/kubernetes/`) — minikube yoki haqiqiy klaster.

📖 Qo'llanmalar: [deploy/docker/README.md](deploy/docker/README.md) · [deploy/docker/HTTPS.md](deploy/docker/HTTPS.md) · [deploy/kubernetes/README.md](deploy/kubernetes/README.md)

### Development Setup (Visual Studio)

1. **Start MinIO** (standalone):
   ```bash
   cd C:\Users\muqim\OneDrive\Ishchi stol\dockerize\minIO
   docker-compose up -d
   ```

2. **Run Backend** in Visual Studio:
   - Set `Forex.WebApi` as startup project
   - Uses `appsettings.Development.json`

3. **Run WPF Client**:
   - Set `Forex.Wpf` as startup project
   - Configure backend URL in `appsettings.json`

## 📦 Features

### File Storage (MinIO)
- Automatic image compression (max 500KB)
- Telegram-style optimization (1920px max dimension)
- Dual-client architecture for Docker networking
- Presigned URL generation for secure uploads

### Image Upload
- **Frontend**: Automatic compression before upload
- **Backend**: 1MB max file size validation
- **Preview**: Instant preview when image selected
- **Quality**: JPEG quality 82% (70% fallback if needed)

## 🔧 Configuration

### Backend (appsettings.json)
```json
{
  "Minio": {
    "Endpoint": "minio:9000",
    "PublicEndpoint": "http://localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "forex-storage"
  },
  "FileUpload": {
    "MaxFileSizeMB": 1
  }
}
```

### Environment Variables (.env)
```bash
# Change for production
MINIO_PUBLIC_ENDPOINT=http://your-server.com:9000
POSTGRES_PASSWORD=strong_password_here
JWT_SECRET_KEY=long_random_secret_key
```

See [deploy/docker/.env.example](deploy/docker/.env.example) for all available options.

## 🌐 API Access

- **Swagger/Scalar UI**: http://localhost:5001/scalar/v1
- **API Endpoint**: http://localhost:5001

## 📁 Project Structure

```
forex/
├── deploy/
│   ├── docker/                 # Docker Compose bilan deploy
│   │   ├── docker-compose.yml      #   minimal: postgres + minio + app
│   │   ├── docker-compose.full.yml #   + pgadmin + backup + traefik (HTTPS)
│   │   ├── .env / .env.example     #   sozlamalar
│   │   ├── traefik/                #   reverse proxy + TLS
│   │   └── README.md / HTTPS.md    #   qo'llanmalar
│   └── kubernetes/             # Kubernetes (minikube / klaster) manifestlari
│       ├── *.yaml              #   namespace, postgres, minio, app, pgadmin, ingress...
│       └── README.md
├── src/
│   ├── backend/
│   │   ├── Forex.Domain/
│   │   ├── Forex.Application/
│   │   ├── Forex.Infrastructure/
│   │   └── Forex.WebApi/
│   │       └── Dockerfile      # Backend image
│   └── frontend/
│       ├── Forex.ClientService/
│       └── Forex.Wpf/
└── README.md
```

## 📝 Technologies

- **.NET 9.0**
- **PostgreSQL** - Database
- **MinIO** - Object Storage
- **WPF** - Desktop Client
- **Docker** - Containerization
- **JWT** - Authentication
- **ImageSharp** - Image Processing

## 📚 Documentation

- [deploy/docker/README.md](deploy/docker/README.md) - Deployment & troubleshooting
- [deploy/docker/HTTPS.md](deploy/docker/HTTPS.md) - HTTPS (Traefik, Cloudflare)
- [deploy/docker/.env.example](deploy/docker/.env.example) - Configuration reference
- API documentation available at `/scalar/v1` when running
