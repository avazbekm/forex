# ============================================================
# Forex uchun self-signed TLS sertifikat (Windows / PowerShell)
# ============================================================
# Windows'da test qilish uchun. Serverda (Linux) generate-cert.sh dan
# foydalaning. openssl o'rnatilgan bo'lishi kerak (Git for Windows bilan keladi).
#
#   .\generate-cert.ps1 -Ip 203.0.113.10

param(
    [Parameter(Mandatory = $true)]
    [string]$Ip
)

$dir = $PSScriptRoot

openssl req -x509 -nodes -newkey rsa:2048 -days 3650 `
    -keyout "$dir/server.key" `
    -out "$dir/server.crt" `
    -subj "/CN=$Ip" `
    -addext "subjectAltName=IP:$Ip"

Write-Host ""
Write-Host "Sertifikat yaratildi (IP=$Ip): $dir/server.crt" -ForegroundColor Green
