# CI/CD and Railway Deployment

This backend is prepared for Railway deployment with Docker.

## What is wired

- `Dockerfile` builds the ASP.NET Core 8 API image.
- `railway.toml` tells Railway to use the Dockerfile and health-check `/health`.
- `docker-compose.yml` runs the API, MongoDB, and Mongo Express for local development.
- `/health` returns a basic API health response.
- `.github/workflows/ci.yml` restores and builds the project on pushes and pull requests.
- `.github/workflows/deploy.yml` builds and publishes a Docker image to GitHub Container Registry on pushes to `main`.

## Deploy to Railway

1. Push this repository to GitHub.
2. In Railway, create a new project.
3. Choose `Deploy from GitHub repo`.
4. Select this backend repository and branch.
5. Railway should detect the root `Dockerfile`.
6. Add the production variables below in the service Variables tab.
7. Open the service Settings tab and generate a Railway domain under Public Networking.
8. Update `AppSettings__BaseUrl`, `MomoSettings__ReturnUrl`, and `MomoSettings__IpnUrl` to use the generated HTTPS domain.

Railway does not run `docker-compose.yml` directly in production. Use Compose locally, and let Railway deploy the API service from the Dockerfile.

## Required Railway Variables

```text
ASPNETCORE_ENVIRONMENT=Production
MongoDbSettings__ConnectionString=<mongodb-connection-string>
MongoDbSettings__DatabaseName=CooTeeDb
Jwt__SecretKey=<at-least-32-characters>
Jwt__Issuer=CooTeeApi
Jwt__Audience=CooTeeClient
Jwt__ExpirationMinutes=60
SmtpSettings__Host=<smtp-host>
SmtpSettings__Port=587
SmtpSettings__Username=<smtp-username>
SmtpSettings__Password=<smtp-password>
SmtpSettings__FromEmail=<sender-email>
SmtpSettings__FromName=CooTee Account
SmtpSettings__EnableSSL=true
AppSettings__BaseUrl=https://your-service.up.railway.app
MomoSettings__PartnerCode=<momo-partner-code>
MomoSettings__AccessKey=<momo-access-key>
MomoSettings__SecretKey=<momo-secret-key>
MomoSettings__Endpoint=<momo-create-payment-endpoint>
MomoSettings__ReturnUrl=https://your-service.up.railway.app/api/orders/momo-return
MomoSettings__IpnUrl=https://your-service.up.railway.app/api/orders/momo-ipn
```

Do not set `ASPNETCORE_URLS` on Railway. Railway injects `PORT`, and the Dockerfile maps that to `ASPNETCORE_URLS` at container startup.

## Local Docker Run

```bash
docker compose up --build
```

Local URLs:

- API: `http://localhost:5001`
- Health: `http://localhost:5001/health`
- Mongo Express: `http://localhost:8081`
