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
8. Update `AppSettings__BaseUrl`, `AppSettings__FrontendBaseUrl`, `MomoSettings__ReturnUrl`, and `MomoSettings__IpnUrl` with the deployed URLs.

Railway does not run `docker-compose.yml` directly in production. Use Compose locally, and let Railway deploy the API service from the Dockerfile.

## Required Railway Variables

```text
ASPNETCORE_ENVIRONMENT=Production
MongoDbSettings__ConnectionString=<mongodb-connection-string>
MongoDbSettings__DatabaseName=CoTeeDB
Jwt__SecretKey=<at-least-32-characters-no-quotes>
Jwt__Issuer=CoTeeApi
Jwt__Audience=CoTeeClient
Jwt__ExpirationMinutes=60
ResendSettings__ApiKey=<resend-api-key>
ResendSettings__ApiBaseUrl=https://api.resend.com
ResendSettings__FromEmail=<verified-resend-sender>
ResendSettings__FromName=CoTee Account
AppSettings__BaseUrl=https://your-service.up.railway.app
AppSettings__FrontendBaseUrl=https://your-frontend.vercel.app
AppSettings__VerificationEmailResendCooldownSeconds=60
AppSettings__AutoVerifyEmailOnRegistration=true
Swagger__Enabled=true
MomoSettings__PartnerCode=<momo-partner-code>
MomoSettings__AccessKey=<momo-access-key>
MomoSettings__SecretKey=<momo-secret-key>
MomoSettings__Endpoint=<momo-create-payment-endpoint>
MomoSettings__ReturnUrl=https://your-service.up.railway.app/api/orders/momo-return
MomoSettings__IpnUrl=https://your-service.up.railway.app/api/orders/momo-ipn
```

For initial Resend testing, `onboarding@resend.dev` can only send to the email address associated with the Resend account. Verify a domain in Resend and use an address on that domain for production delivery.

`AppSettings__AutoVerifyEmailOnRegistration=true` temporarily bypasses verification emails. Set it to `false` after Resend is ready.

For databases initialized by an older version, remove the user-deleting token TTL index once:

```javascript
db.users.dropIndex("token_expiration_ttl")
db.users.createIndex({ verificationToken: 1 }, { sparse: true, name: "verification_token_index" })
db.users.createIndex({ passwordResetToken: 1 }, { sparse: true, name: "password_reset_token_index" })
```

Do not set `ASPNETCORE_URLS` on Railway. Railway injects `PORT`, and the Dockerfile maps that to `ASPNETCORE_URLS` at container startup.

Swagger is available at:

```text
https://your-service.up.railway.app/swagger
```

Swagger is enabled by default in production for this project. You can set `Swagger__Enabled=false` in Railway later if you do not want public API docs.

## Common Railway Startup Error

If Railway logs show this:

```text
JwtSettings.SecretKey must be at least 32 characters
```

Fix the service variable named exactly:

```text
Jwt__SecretKey
```

Do not use `JWT_SECRET_KEY`, `Jwt:SecretKey`, or `${JWT_SECRET_KEY}` on Railway. ASP.NET Core maps nested config using double underscores.

Use a long random value, for example:

```text
Jwt__SecretKey=change-this-to-a-random-secret-with-64-plus-characters
```

After changing the variable, redeploy the Railway service.

## MongoDB Atlas

For MongoDB Atlas, set the connection string in Railway as:

```text
MongoDbSettings__ConnectionString=mongodb+srv://<username>:<password>@<cluster-host>/
MongoDbSettings__DatabaseName=CoTeeDB
```

Keep the database name in `MongoDbSettings__DatabaseName`. The app will create collections when it first writes data.

Do not commit the real Atlas username or password to this repository.

## Local Docker Run

```bash
docker compose up --build
```

Local URLs:

- API: `http://localhost:5001`
- Health: `http://localhost:5001/health`
- Mongo Express: `http://localhost:8081`
