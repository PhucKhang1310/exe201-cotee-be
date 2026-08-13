# Deployment

This backend is prepared for deployment on a Linode VPS with Docker Compose and MongoDB. By default it runs behind an existing host reverse proxy, which is the right setup when the same Linode already serves another backend on ports `80` and `443`.

## What is wired

- `Dockerfile` builds the ASP.NET Core 8 API image.
- `heroku.yml` lets Heroku build the Docker image with the container stack.
- `docker-compose.prod.yml` runs the API and MongoDB in production.
- The API binds to `127.0.0.1:${API_PUBLIC_PORT:-5002}` so an existing Nginx/Caddy proxy can route traffic to it.
- `deploy/Caddyfile` is available only for a standalone server where this project owns ports `80` and `443`.
- `.env.production.example` documents the production variables for the Linode server.
- `.env.heroku.example` documents Heroku config vars. Do not commit real secrets.
- `docker-compose.yml` still runs the API, MongoDB, and Mongo Express for local development.
- `/health` returns a basic API health response.
- `.github/workflows/ci.yml` restores and builds the project on pushes and pull requests.
- `.github/workflows/deploy.yml` builds and publishes a Docker image to GitHub Container Registry on pushes to `main`.

## Deploy to Heroku

Heroku is configured to build this backend from `Dockerfile` through `heroku.yml`. The Dockerfile already starts ASP.NET Core on `0.0.0.0:${PORT:-8080}`; Heroku injects `PORT` at runtime.

Use MongoDB Atlas or another hosted MongoDB provider. Heroku dynos do not run this repo's `mongodb` Docker Compose service.

1. Create the Heroku app from this backend repo:

   ```bash
   heroku login
   heroku create <your-heroku-app-name>
   heroku stack:set container -a <your-heroku-app-name>
   ```

2. Set the required config vars. Start from `.env.heroku.example` and replace every placeholder:

   ```bash
   heroku config:set -a <your-heroku-app-name> \
     ASPNETCORE_ENVIRONMENT=Production \
     Swagger__Enabled=true \
     MongoDbSettings__ConnectionString='mongodb+srv://<username>:<password>@<cluster-host>/' \
     MongoDbSettings__DatabaseName=CoTeeDB \
     Jwt__SecretKey='<at-least-32-characters-no-quotes>' \
     Jwt__Issuer=CoTeeApi \
     Jwt__Audience=CoTeeClient \
     Jwt__ExpirationMinutes=60 \
     ResendSettings__ApiKey='<resend-api-key>' \
     ResendSettings__ApiBaseUrl=https://api.resend.com \
     ResendSettings__FromEmail='<verified-resend-sender>' \
     ResendSettings__FromName='CoTee Account' \
     AppSettings__BaseUrl='https://<your-heroku-app-name>.herokuapp.com' \
     AppSettings__FrontendBaseUrl='https://exe101-cotee-fe.vercel.app' \
     AppSettings__VerificationEmailResendCooldownSeconds=60 \
     AppSettings__AutoVerifyEmailOnRegistration=true \
     Google__Enabled=false \
     MomoSettings__PartnerCode='<momo-partner-code>' \
     MomoSettings__AccessKey='<momo-access-key>' \
     MomoSettings__SecretKey='<momo-secret-key>' \
     MomoSettings__Endpoint='<momo-create-payment-endpoint>' \
     MomoSettings__RedirectUrl='https://exe101-cotee-fe.vercel.app/payment-result' \
     MomoSettings__IpnUrl='https://<your-heroku-app-name>.herokuapp.com/api/orders/momo-ipn' \
     MomoSettings__PartnerName=CoTee \
     MomoSettings__StoreId=CoTeeStore \
     MomoSettings__Language=vi \
     OpenAi__ApiKey='<openai-api-key>' \
     OpenAi__ChatModel=gpt-4o-mini \
     OpenAi__ImageModel=gpt-image-1.5 \
     OpenAi__UseMock=false
   ```

   If you do not want OpenAI image/chat calls during early smoke testing, set `OpenAi__UseMock=true`.

3. Deploy the current branch:

   ```bash
   git push heroku deploy/heroku-backend:main
   ```

4. Scale the web dyno and check the deployment:

   ```bash
   heroku ps:scale web=1 -a <your-heroku-app-name>
   heroku logs --tail -a <your-heroku-app-name>
   curl https://<your-heroku-app-name>.herokuapp.com/health
   ```

Swagger is available at:

```text
https://<your-heroku-app-name>.herokuapp.com/swagger/index.html
```

For a custom domain, add it in Heroku and then update:

```bash
heroku config:set -a <your-heroku-app-name> \
  AppSettings__BaseUrl=https://api.yourdomain.com \
  MomoSettings__IpnUrl=https://api.yourdomain.com/api/orders/momo-ipn
```

## Deploy to Linode

1. Create a Linode running Ubuntu 22.04 or 24.04.
2. Point your DNS record, for example `exe201-api.cotee.xyz`, to the Linode public IPv4 address.
3. SSH into the server and install Docker:

   ```bash
   sudo apt update
   sudo apt install -y ca-certificates curl
   sudo install -m 0755 -d /etc/apt/keyrings
   sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
   sudo chmod a+r /etc/apt/keyrings/docker.asc
   echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
   sudo apt update
   sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
   ```

4. Open the firewall for SSH, HTTP, and HTTPS if this server is not already configured:

   ```bash
   sudo ufw allow OpenSSH
   sudo ufw allow 80/tcp
   sudo ufw allow 443/tcp
   sudo ufw enable
   ```

5. Clone or upload this backend folder to the server.
6. Create the production env file:

   ```bash
   cd exe201-cotee-be
   cp .env.production.example .env.production
   nano .env.production
   ```

7. Replace the sample domain, frontend URLs, and every secret in `.env.production`. If `api.cotee.xyz` is already used by another backend, use a separate subdomain such as `exe201-api.cotee.xyz`.
8. Start the production stack:

   ```bash
   docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
   ```

9. Add a route to your existing host reverse proxy.

   Caddy example:

   ```caddyfile
   exe201-api.cotee.xyz {
     reverse_proxy 127.0.0.1:5002
   }
   ```

   Nginx example:

   ```nginx
   server {
     listen 80;
     server_name exe201-api.cotee.xyz;

     location / {
       proxy_pass http://127.0.0.1:5002;
       proxy_set_header Host $host;
       proxy_set_header X-Real-IP $remote_addr;
       proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
       proxy_set_header X-Forwarded-Proto $scheme;
     }
   }
   ```

10. Check the deployment:

   ```bash
   docker compose --env-file .env.production -f docker-compose.prod.yml ps
   docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api
   curl http://127.0.0.1:5002/health
   curl https://exe201-api.cotee.xyz/health
   ```

## Required Production Variables

```text
ASPNETCORE_ENVIRONMENT=Production
API_DOMAIN=exe201-api.cotee.xyz
API_PUBLIC_PORT=5002
MONGO_ROOT_USERNAME=<mongodb-root-username>
MONGO_ROOT_PASSWORD=<mongodb-root-password>
MONGO_DATABASE=CoTeeDB
Jwt__SecretKey=<at-least-32-characters-no-quotes>
Jwt__Issuer=CoTeeApi
Jwt__Audience=CoTeeClient
Jwt__ExpirationMinutes=60
ResendSettings__ApiKey=<resend-api-key>
ResendSettings__ApiBaseUrl=https://api.resend.com
ResendSettings__FromEmail=<verified-resend-sender>
ResendSettings__FromName=CoTee Account
AppSettings__BaseUrl=https://exe201-api.cotee.xyz
AppSettings__FrontendBaseUrl=https://your-frontend.vercel.app
AppSettings__VerificationEmailResendCooldownSeconds=60
AppSettings__AutoVerifyEmailOnRegistration=true
Swagger__Enabled=true
MomoSettings__PartnerCode=<momo-partner-code>
MomoSettings__AccessKey=<momo-access-key>
MomoSettings__SecretKey=<momo-secret-key>
MomoSettings__Endpoint=<momo-create-payment-endpoint>
MomoSettings__RedirectUrl=https://cotee.xyz/payment-result
MomoSettings__IpnUrl=https://exe201-api.cotee.xyz/api/orders/momo-ipn
```

For initial Resend testing, `onboarding@resend.dev` can only send to the email address associated with the Resend account. Verify a domain in Resend and use an address on that domain for production delivery.

`AppSettings__AutoVerifyEmailOnRegistration=true` temporarily bypasses verification emails. Set it to `false` after Resend is ready.

For databases initialized by an older version, remove the user-deleting token TTL index once:

```javascript
db.users.dropIndex("token_expiration_ttl")
db.users.createIndex({ verificationToken: 1 }, { sparse: true, name: "verification_token_index" })
db.users.createIndex({ passwordResetToken: 1 }, { sparse: true, name: "password_reset_token_index" })
```

The API listens on container port `8080` and is published only to localhost on `API_PUBLIC_PORT`, default `5002`. Your existing reverse proxy should be the public entry point.

Swagger is available at:

```text
https://exe201-api.cotee.xyz/swagger
```

Swagger is enabled by default in production for this project. Set `Swagger__Enabled=false` in `.env.production` if you do not want public API docs.

## Common Startup Error

If logs show this:

```text
JwtSettings.SecretKey must be at least 32 characters
```

Fix the service variable named exactly:

```text
Jwt__SecretKey
```

Do not use `JWT_SECRET_KEY`, `Jwt:SecretKey`, or `${JWT_SECRET_KEY}`. ASP.NET Core maps nested config using double underscores.

Use a long random value, for example:

```text
Jwt__SecretKey=change-this-to-a-random-secret-with-64-plus-characters
```

After changing the variable, restart the API:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d api
```

## Updating the App

If you build on the Linode from source:

```bash
git pull
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

If you use the GitHub Container Registry image published by `.github/workflows/deploy.yml`, set this in `.env.production`:

```text
COTEE_API_IMAGE=ghcr.io/<owner>/<repo>/cotee-api:latest
```

Then update with:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml pull api
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
```

## Standalone Caddy Mode

Use this only when this project should own ports `80` and `443` on the Linode:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml --profile standalone-proxy up -d --build
```

Do not use `standalone-proxy` when another Caddy, Nginx, Traefik, or Docker container already listens on ports `80` or `443`.

## MongoDB Atlas

The production Compose file runs MongoDB on the Linode and keeps it private inside the Docker network. If you prefer MongoDB Atlas, replace the API connection string override in `docker-compose.prod.yml` or add this directly to `.env.production` and remove the Compose override:

```text
MongoDbSettings__ConnectionString=mongodb+srv://<username>:<password>@<cluster-host>/
MongoDbSettings__DatabaseName=CoTeeDB
```

Keep the database name in `MongoDbSettings__DatabaseName`. The app will create collections when it first writes data.

Do not commit the real Atlas username or password to this repository.

## Backups

Back up the MongoDB volume before upgrades or server migrations:

```bash
docker exec cotee-exe201-mongodb mongodump --username "$MONGO_ROOT_USERNAME" --password "$MONGO_ROOT_PASSWORD" --authenticationDatabase admin --out /tmp/cotee-backup
docker cp cotee-exe201-mongodb:/tmp/cotee-backup ./cotee-backup
```

## Local Docker Run

```bash
docker compose up --build
```

Local URLs:

- API: `http://localhost:5001`
- Health: `http://localhost:5001/health`
- Mongo Express: `http://localhost:8081`
