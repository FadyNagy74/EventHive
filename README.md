# EventHive — Distributed Event Booking Microservices Platform

EventHive is an enterprise-grade, distributed microservices platform built with **.NET 8**, designed to demonstrate modern cloud-native patterns including API Gateway routing (YARP), dynamic service discovery (Consul), asynchronous event-driven architecture (MassTransit & RabbitMQ with delayed message scheduling), resilient inter-service communication (Polly Circuit Breakers), Google OAuth 2.0 / JWT security, and third-party payment integration with Paymob.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Service Map & Ports](#2-service-map--ports)
3. [Prerequisites](#3-prerequisites)
4. [External Accounts & Third-Party Setup](#4-external-accounts--third-party-setup)
   - [Google Cloud Console (OAuth 2.0)](#41-google-cloud-console-oauth-20)
   - [Paymob Dashboard Setup](#42-paymob-dashboard-setup)
5. [Configuration & User Secrets](#5-configuration--user-secrets)
6. [Database Setup & Migrations](#6-database-setup--migrations)
7. [Step-by-Step Launch Sequence](#7-step-by-step-launch-sequence)
   - [Step 1: Start Infrastructure Containers (Docker Compose)](#step-1-start-infrastructure-containers-docker-compose)
   - [Step 2: Start the Microservices](#step-2-start-the-microservices)
   - [Step 3: Expose Payment Webhook via ngrok](#step-3-expose-payment-webhook-via-ngrok)
8. [End-to-End Testing Flow](#8-end-to-end-testing-flow)
9. [Resilience & Circuit Breakers](#9-resilience--circuit-breakers)
10. [Troubleshooting & FAQs](#10-troubleshooting--faqs)

---

## 1. Architecture Overview

```
                                +---------------------------------------------+
                                |               Internet / Client             |
                                +----------------------+----------------------+
                                                       | HTTPS (:7107)
                                                       v
                                +---------------------------------------------+
                                |              API Gateway (YARP)              |
                                |     Path-based routing to private network    |
                                +----+----------+----------+----------+-------+
                                     |          |          |          |
                    +----------------+          |          |          +----------------+
                    | HTTP (:5008)              | HTTP     | HTTP                      | HTTP (:5190)
                    v                           v (:5041)  v (:5118)                   v
          +-------------------+        +-------------+  +--------------+     +-------------------+
          |  Authentication   |        |   Catalog   |  |   Booking    |     |      Payment      |
          |      Service      |        |   Service   |  |   Service    |     |      Service      |
          +---------+---------+        +------+------+  +------+-------+     +---------+---------+
                    |                         ^                |                       |
                    | (Validates JWT)         | (Sync HTTP)    |                       |
                    +-------------------------+----------------+                       |
                                                               |                       |
                                               +---------------+-----------------------+
                                               |             Async Events (RabbitMQ)
                                               v
                                      +-----------------+             +---------------------+
                                      |    RabbitMQ     |<----------->|    Notification     |
                                      | (Topic Exchange)|             |       Service       |
                                      +-----------------+             +----------+----------+
                                               ^                                 |
                                               |                                 v
                                      +--------+--------+             +---------------------+
                                      |  Consul Server  |             |       MongoDB       |
                                      | Service Registry|             | (Notification Audit)|
                                      +-----------------+             +---------------------+
```

### Core Architecture Highlights
- **Perimeter Security:** TLS terminates at the API Gateway (`https://localhost:7107`). All downstream microservices run on internal HTTP on a private network (`RequireHttpsMetadata = false`).
- **Dynamic Service Discovery:** Services register themselves with **Consul** on startup. YARP periodically queries Consul for healthy instances to route traffic.
- **Asynchronous Event-Driven Decoupling:** RabbitMQ (via MassTransit) handles domain lifecycle events (`BookingCreated`, `PaymentSucceeded`, `PaymentFailed`, `BookingExpired`, `EventCancelled`) using topic exchanges.
- **Delayed Message Scheduling:** Expire-booking timer runs via RabbitMQ's Delayed Message Exchange plugin (`x-delayed-message`) to automatically release abandoned seat holds after 15 minutes.
- **Circuit Breakers & Fault Tolerance:** Polly-based resilience pipelines prevent cascading failures during internal Catalog dependency or external Paymob gateway degradation.
- **Polyglot Persistence:** SQL Server per transactional service (Auth, Catalog, Booking, Payment) and MongoDB for event-driven notification auditing.

---

## 2. Service Map & Ports

| Service / Container | Type | Host Port | Role & Description |
| :--- | :--- | :--- | :--- |
| **API Gateway** | Microservice | `5257` (HTTP) / **`7107` (HTTPS)** | Public entry point; YARP reverse proxy; JWT claims forwarding |
| **AuthenticationService** | Microservice | `5008` (HTTP) / `7158` (HTTPS) | Google OAuth2 sign-in, JWT token generation & refresh |
| **CatalogService** | Microservice | `5041` (HTTP) / `7278` (HTTPS) | Event management, capacity tracking, atomic seat reservations |
| **BookingService** | Microservice | `5118` (HTTP) / `7108` (HTTPS) | Booking lifecycle, held inventory orchestrator, delayed auto-expiry |
| **PaymentService** | Microservice | `5190` (HTTP) / `7051` (HTTPS) | Paymob intention creation, webhook receiver & HMAC verification |
| **NotificationService**| Microservice | `5059` (HTTP) / `7099` (HTTPS) | Event consumer, user notification dispatcher (MongoDB backed) |
| **Consul Server** | Docker | **`8500`** (UI/HTTP) | Service registry, health check monitor (`/health`) |
| **RabbitMQ** | Docker | **`5672`** (AMQP) / **`15672`** (UI)| Message broker with delayed exchange plugin (`guest`/`guest`) |
| **MongoDB** | Docker | **`27017`** | Document store for notification idempotency and logs |

> **Note:** All external client requests and Postman tests must be sent to the **API Gateway** on **`https://localhost:7107`**.

---

## 3. Prerequisites

Ensure the following tools are installed on your machine:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet --version`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Required for Consul, RabbitMQ, and MongoDB)
- **SQL Server** (LocalDB, SQL Express, or Developer Edition)
- [ngrok CLI](https://ngrok.com/download) (Required to expose the local Payment webhook to Paymob)
- [Postman](https://www.postman.com/downloads/) (For testing the APIs via the included collection)

---

## 4. External Accounts & Third-Party Setup

### 4.1 Google Cloud Console (OAuth 2.0)
Authentication relies on Google OAuth 2.0 to issue internal JWTs:
1. Open [Google Cloud Console](https://console.cloud.google.com/).
2. Create or select a project and go to **APIs & Services $\rightarrow$ Credentials**.
3. Click **Create Credentials $\rightarrow$ OAuth client ID** (Application type: **Web application**).
4. Under **Authorized redirect URIs**, add:
   - `https://localhost:7107/api/authentication/google-response`
   - `https://localhost:7158/api/authentication/google-response` *(for direct local testing)*
5. Save your **Client ID** and **Client Secret**.

---

### 4.2 Paymob Dashboard Setup
Payment processing uses Paymob's Unified Checkout and Webhooks:
1. Sign up / Log in to your [Paymob Dashboard](https://accept.paymob.com/).
2. **Retrieve API Keys:**
   - Navigate to **Settings $\rightarrow$ Account Info** or **Developers $\rightarrow$ API Keys**.
   - Copy the following values:
     - **API Key**
     - **Secret Key** (`Token <SecretKey>`)
     - **Public Key**
     - **HMAC Secret** *(found under Settings $\rightarrow$ Account Info / Developers $\rightarrow$ HMAC)*
3. **Create an Online Card Integration:**
   - Go to **Developers $\rightarrow$ Payment Integrations**.
   - Click **Add Integration** and select **Card / Online Card**.
   - Note the generated **Integration ID** (e.g., `1234567`).
4. **Configure Integration Callbacks:**
   - In the integration settings:
     - **Transaction Response Callback (Redirection URL):** Set to `https://mockpaymentpage.web.app` (our hosted payment completion page).
     - **Transaction Processed Callback (Webhook URL):** Set to your ngrok tunnel URL + `/webhook/payment` (e.g., `https://<ngrok-id>.ngrok-free.app/webhook/payment`). *You will update this URL when running ngrok in Step 3 of the launch flow.*

---

## 5. Configuration & User Secrets

Sensitive configuration (database connection strings, JWT secret keys, OAuth secrets, and Paymob tokens) is managed via **.NET User Secrets** so it is never committed to Git.

> [!IMPORTANT]
> The `JWT:SecretKey` **must be identical** across all microservices because internal services validate the symmetric token issued by `AuthenticationService`.

Replace `<YOUR_SQL_SERVER_INSTANCE>` with your local instance (for example `localhost` or `.\SQLEXPRESS`).

### 5.1 AuthenticationService
Open a terminal in `AuthenticationService/`:
```powershell
cd AuthenticationService

dotnet user-secrets set "JWT:SecretKey" "<YOUR_256_BIT_SECRET_KEY>"
dotnet user-secrets set "JWT:Issuer" "EventHive"
dotnet user-secrets set "JWT:Audience" "EventHive"
dotnet user-secrets set "Authentication:Google:ClientId" "<YOUR_GOOGLE_CLIENT_ID>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<YOUR_GOOGLE_CLIENT_SECRET>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<YOUR_SQL_SERVER_INSTANCE>;Database=EventHiveAuth;Trusted_Connection=True;TrustServerCertificate=true;"
```

### 5.2 CatalogService
Open a terminal in `CatalogService/`:
```powershell
cd CatalogService

dotnet user-secrets set "JWT:SecretKey" "<YOUR_256_BIT_SECRET_KEY>"
dotnet user-secrets set "JWT:Issuer" "EventHive"
dotnet user-secrets set "JWT:Audience" "EventHive"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<YOUR_SQL_SERVER_INSTANCE>;Database=EventHiveCatalog;Trusted_Connection=True;TrustServerCertificate=true;"
dotnet user-secrets set "RabbitMQ:Username" "catalog_service"
dotnet user-secrets set "RabbitMQ:Password" "securecatalog123"
```

### 5.3 BookingService
Open a terminal in `BookingService/`:
```powershell
cd BookingService

dotnet user-secrets set "JWT:SecretKey" "<YOUR_256_BIT_SECRET_KEY>"
dotnet user-secrets set "JWT:Issuer" "EventHive"
dotnet user-secrets set "JWT:Audience" "EventHive"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<YOUR_SQL_SERVER_INSTANCE>;Database=EventHiveBooking;Trusted_Connection=True;TrustServerCertificate=true;"
dotnet user-secrets set "RabbitMQ:Username" "booking_service"
dotnet user-secrets set "RabbitMQ:Password" "securebooking123"
```

### 5.4 PaymentService
Open a terminal in `PaymentService/`:
```powershell
cd PaymentService

dotnet user-secrets set "JWT:SecretKey" "<YOUR_256_BIT_SECRET_KEY>"
dotnet user-secrets set "JWT:Issuer" "EventHive"
dotnet user-secrets set "JWT:Audience" "EventHive"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<YOUR_SQL_SERVER_INSTANCE>;Database=EventHivePayment;Trusted_Connection=True;TrustServerCertificate=true;"
dotnet user-secrets set "RabbitMQ:Username" "payment_service"
dotnet user-secrets set "RabbitMQ:Password" "securepayment123"
dotnet user-secrets set "Paymob:BaseUrl" "https://accept.paymob.com"
dotnet user-secrets set "Paymob:SecretKey" "<YOUR_PAYMOB_SECRET_KEY>"
dotnet user-secrets set "Paymob:PublicKey" "<YOUR_PAYMOB_PUBLIC_KEY>"
dotnet user-secrets set "Paymob:IntegrationId" "<YOUR_PAYMOB_INTEGRATION_ID>"
dotnet user-secrets set "Paymob:HmacSecret" "<YOUR_PAYMOB_HMAC_SECRET>"
dotnet user-secrets set "Paymob:RedirectionUrl" "https://mockpaymentpage.web.app"
```

### 5.5 NotificationService
Open a terminal in `NotificationService/`:
```powershell
cd NotificationService

dotnet user-secrets set "JWT:SecretKey" "<YOUR_256_BIT_SECRET_KEY>"
dotnet user-secrets set "JWT:Issuer" "EventHive"
dotnet user-secrets set "JWT:Audience" "EventHive"
dotnet user-secrets set "RabbitMQ:Username" "notification_service"
dotnet user-secrets set "RabbitMQ:Password" "securenotification123"
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "MongoDb:DatabaseName" "EventHiveNotification"
```

---

## 6. Database Setup & Migrations

Apply Entity Framework Core database migrations for all SQL Server microservices:

```powershell
# 1. Authentication Service DB
cd AuthenticationService
dotnet ef database update

# 2. Catalog Service DB
cd ..\CatalogService
dotnet ef database update

# 3. Booking Service DB
cd ..\BookingService
dotnet ef database update

# 4. Payment Service DB
cd ..\PaymentService
dotnet ef database update
```

*(NotificationService uses MongoDB, which initializes its collections and indexes automatically on first write).*

### Assigning the Admin Role
The system automatically seeds `Admin` and `User` roles. To promote a user to `Admin` after their first Google sign-in:
```sql
-- Run in the EventHiveAuth database
DECLARE @userId NVARCHAR(450) = (SELECT Id FROM AspNetUsers WHERE Email = 'your-email@gmail.com');
DECLARE @adminRoleId NVARCHAR(450) = (SELECT Id FROM AspNetRoles WHERE NormalizedName = 'ADMIN');

INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT @userId, @adminRoleId
WHERE NOT EXISTS (
    SELECT 1 FROM AspNetUserRoles WHERE UserId = @userId AND RoleId = @adminRoleId
);
```
*(After running the script, re-login via Google to receive a fresh JWT containing the `Admin` role claim).*

---

## 7. Step-by-Step Launch Sequence

Follow this exact 3-step sequence to launch the entire system:

### Step 1: Start Infrastructure Containers (Docker Compose)
From the solution root directory (`EventHive/`), run:

```powershell
docker-compose up -d
```

This starts the 3 required infrastructure containers:
1. **Consul Server (`consul-server`)**: http://localhost:8500
2. **RabbitMQ (`rabbitmq`)**: AMQP on port `5672`, Management Dashboard on http://localhost:15672 (`guest` / `guest`). Built with the delayed-message exchange plugin.
3. **MongoDB (`mongodb`)**: Listening on port `27017`.

Verify all 3 containers are healthy via `docker ps`.

---

### Step 2: Start the Microservices
Open a terminal for each microservice and start them using the `https` profile.

> **Tip:** Start the downstream microservices first so they register in Consul before starting the API Gateway.

```powershell
# Terminal 1 — Authentication Service
cd AuthenticationService
dotnet run --launch-profile https

# Terminal 2 — Catalog Service
cd CatalogService
dotnet run --launch-profile https

# Terminal 3 — Booking Service
cd BookingService
dotnet run --launch-profile https

# Terminal 4 — Payment Service
cd PaymentService
dotnet run --launch-profile https

# Terminal 5 — Notification Service
cd NotificationService
dotnet run --launch-profile https

# Terminal 6 — API Gateway (Start Last)
cd ApiGateway
dotnet run --launch-profile https
```

Check the **API Gateway console output** — you should see:
```text
YARP routing updated: 5 routes, 5 clusters.
```
*(If you see fewer than 5 clusters, wait ~10 seconds for Consul health checks to mark all services as passing).*

---

### Step 3: Expose Payment Webhook via ngrok
Paymob sends an asynchronous server-to-server webhook callback when a transaction completes. Expose the **PaymentService** HTTP port (`5190`) to the public internet:

```bash
ngrok http 5190
```

1. Copy the public forwarding HTTPS URL from ngrok (e.g. `https://abc1-23-45-67.ngrok-free.app`).
2. Go to your **Paymob Dashboard $\rightarrow$ Developers $\rightarrow$ Payment Integrations $\rightarrow$ Edit Integration**.
3. Under **Transaction Processed Callback**, update the URL to:
   ```text
   https://<your-ngrok-subdomain>.ngrok-free.app/webhook/payment
   ```
4. Save the integration.

---

## 8. End-to-End Testing Flow

Import the included **`EventHive.postman_collection.json`** into Postman. All requests route through the Gateway (`https://localhost:7107`).

### 1. Authenticate via Google OAuth
1. Open your browser and navigate to:
   ```text
   https://localhost:7107/api/authentication/google-login
   ```
2. Complete Google Sign-In.
3. You will receive a JSON response containing `accessToken`, `refreshToken`, and user details.
4. Copy the `accessToken` value and paste it into the **`access_token`** collection variable in Postman.

### 2. Create & Publish an Event (Admin)
1. In Postman, execute `POST /api/catalog/event` with event details.
2. Copy the returned `id` and paste it into Postman's `event_id` collection variable.
3. Execute `PUT /api/catalog/event/publish/{{event_id}}` to make it bookable.

### 3. Create a Booking
1. Execute `POST /api/booking`:
   ```json
   {
     "eventId": "{{event_id}}",
     "eventName": "Tech Conference 2026",
     "quantity": 2
   }
   ```
2. `BookingService` performs an atomic synchronous seat reservation with `CatalogService` and creates a `Pending` booking.
3. `BookingCreated` event is published to RabbitMQ.
4. `PaymentService` consumes the event, creates a Paymob payment intention, and stores a `CheckoutUrl`.
5. `NotificationService` consumes the event and creates an initial pending notification record in MongoDB.

### 4. Complete Payment
1. Fetch your booking payment details via `GET /api/payment/booking/{{booking_id}}`.
2. Open the returned `checkoutUrl` in your browser.
3. Enter Paymob test card credentials (available in Paymob documentation) and submit.
4. Paymob processes the payment and redirects your browser to `https://mockpaymentpage.web.app/payment-result.html`.
5. Simultaneously, Paymob posts a webhook to `https://<ngrok-id>.ngrok-free.app/webhook/payment`.
6. `PaymentService` verifies the HMAC signature and publishes `PaymentSucceeded`.
7. `BookingService` consumes `PaymentSucceeded` and transitions the booking to `Confirmed`.
8. `NotificationService` consumes `PaymentSucceeded`, marks the notification as sent, and dispatches the confirmation email/log.

---

## 9. Resilience & Circuit Breakers

EventHive incorporates Polly resilience pipelines (via `Microsoft.Extensions.Http.Resilience`) to protect against cascading failures:

1. **BookingService $\rightarrow$ CatalogService (`catalog-pipeline`):**
   - **Timeout:** 5 seconds per HTTP request.
   - **Circuit Breaker:** Breaks for 25 seconds if 50% of requests fail across a 10-second sampling window (minimum 5 calls).
   - **Graceful Degradation:** Returns HTTP `503 Service Unavailable` with standard message `"Catalog service is temporarily unavailable. Please try again later."`.
2. **PaymentService $\rightarrow$ Paymob (`paymob-pipeline`):**
   - **Timeout:** 6 seconds per request.
   - **Circuit Breaker:** Breaks for 30 seconds if 50% of requests fail across a 15-second sampling window (minimum 5 calls).
   - **Graceful Degradation:** Catches `BrokenCircuitException`, stores a `Failed` payment record, and publishes `PaymentFailed` with standard message `"Payment gateway is temporarily unavailable. Please try again later."`, allowing `BookingService` to release held seats and `NotificationService` to inform the customer.

---

## 10. Troubleshooting & FAQs

- **YARP Shows `0 clusters`:**
  - *Cause:* Downstream services have not finished registering with Consul or failed health checks.
  - *Fix:* Ensure microservices are running and verify their health check returns `200 OK` on `http://localhost:<ServicePort>/health`. Check Consul UI at http://localhost:8500.
- **Paymob Webhook Returns 401 Unauthorized:**
  - *Cause:* HMAC secret mismatch.
  - *Fix:* Verify that `Paymob:HmacSecret` in `PaymentService` user-secrets matches the HMAC secret displayed in your Paymob dashboard.
- **RabbitMQ Delayed Exchange Errors:**
  - *Cause:* Default RabbitMQ image does not have the delayed-message plugin installed.
  - *Fix:* Always run the included `Dockerfile.rabbitmq` via `docker-compose up -d`, which compiles with `rabbitmq_delayed_message_exchange`.
- **SSL / Certificate Warnings:**
  - *Cause:* Local ASP.NET Core dev certificate untrusted.
  - *Fix:* Run `dotnet dev-certs https --trust` in an administrative terminal.
