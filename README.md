# Smart Roots Server

> **The brain behind the Smart Roots ecosystem. A robust C#/.NET backend that connects IoT hardware, MQTT, databases, cloud storage, and the Smart Roots App — with a laser focus on security, performance, and extensibility.**

---

## 🌐 Table of Contents

1. [Project Overview](#project-overview)
2. [Features & External Integrations](#features--external-integrations)
3. [High-Level Architecture & Technology Stack](#high-level-architecture--technology-stack)
    - [API Endpoints](#api-endpoints)
    - [Tent Management Flow](#tent-management-flow)
    - [Sensor & Device Operations](#sensor--device-operations)
    - [Image Handling](#image-handling)
    - [Authentication & Security](#authentication--security)
    - [Database & Storage](#database--storage)
    - [Cloud-Native, Docker-Ready](#cloud-native-docker-ready)
    - [External Packages & Services](#external-packages--services)
4. [Repository & Code Structure](#repository--code-structure)
5. [How to Contribute](#how-to-contribute)
6. [How to Run (Dev & Production)](#how-to-run-dev--production)
7. [Room for Expansion](#room-for-expansion)
8. [License](#license)
9. [Reference Links](#reference-links)

---

## 📜 Project Overview

Smart Roots Server is a modern, microservices-inspired backend system for smart agriculture and hydroponics automation.  
It orchestrates sensor data, image uploads, secure device toggling, real-time event streams, and tent/user management for the broader Smart Roots ecosystem.  
It acts as the main API for the Smart Roots App, with an architecture built for *IoT scale-out, cloud reliability, and strong data protection*.

---

## ✨ Features & External Integrations

| Category           | Details                                                                                                                                    |
|--------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| **Tent API**       | Create, update, delete, fetch, or publicly list tents with full metadata & hashed passwords.                                               |
| **Sensor API**     | Real-time streaming (SSE) from hardware via MQTT; all sensor state changes and control actions exposed as structured endpoints.            |
| **Device Control** | Toggle pumps, fans, lights, extractor fans, EC/pH actuators, etc. through MQTT JSON publishing; validated schemas for actuator state.      |
| **Image Handling** | Accepts Base64 image upload, stores securely in Supabase Storage, exposes public URLs, and provides "latest image" query for tents/devices.|
| **Auth/Roles**     | Uses Supabase JWT and custom logic for login, registration, claims validation, and RBAC (role-based access control).                       |
| **Cloud Storage**  | All non-relational logs (sensor stream) in MongoDB; tent, image, and config data in Supabase/Postgres/Storage buckets.                     |
| **Extensibility**  | Fully modular — add more endpoints/controllers/repos/devices without codebase refactor.                                                    |
| **DevOps Ready**   | Ship and scale with Docker; robust `.dockerignore`. Strong, cloud-agnostic configuration.                                                  |

---

## 🏗️ High-Level Architecture & Technology Stack

### API Endpoints

- **/api/tents**
    - GET: List all tents (public) or fetch by MAC address.
    - POST: Create tent (`TentCreateDto`) — secure MAC, password hash, type, etc.
    - PUT: Update tent info or type (with validation, protected by RBAC).
    - DELETE: Remove tent by MAC (secure, role-protected).
    - POST `/verify-password`: Auth check for tent access.
- **/api/sensors**
    - GET: Live stream sensor reading events (SSE) — tent-filtered or broadcast.
    - POST `/toggle`: Change state of tent actuators, published via MQTT.
- **/api/images**
    - POST: Upload image (Base64 JPEG); stores in Supabase, returns URL.
    - GET: Fetch latest image for a given tent/MAC.

### Tent Management Flow

- All tent data is normalized (unique MAC required, validated with regex).
- Password validation/hashing on entry; stored hashed (never plaintext).
- Metadata fields: Name, Location, Country, Organization (for multi-client support), TentType ("veg" or "fodder", for app logic).

### Sensor & Device Operations

- MQTT events for low-latency device reads/toggles.
- SSE for reliable client push (app always sees latest tent/device state).
- Device schema (see `SensorStates.cs`) supports all actuator types; new ones easy to extend.

### Image Handling

- Images POSTed as Base64, stripped and checked, converted to bytes, and uploaded to Supabase Storage with tent/MAC-based path.
- Public image URLs generated for later use by client apps.
- Always fetches the latest per-tent image.

### Authentication & Security

- Multi-token auth with Supabase: supports both AccessToken and RefreshToken flows, including role extraction for private/protected endpoints.
- Custom RBAC wrappers for each method group to pin allowed roles (future extensibility for finer permissions).
- All passwords hashed server-side using `PasswordHasher`.

### Database & Storage

- **Supabase/Postgres**: tent metadata, images, configuration.
- **Supabase Storage**: image blobs — organized per tent/MAC address.
- **MongoDB (via official driver)**: event/sensor logs, analyzable at scale.
- **Cloud MQTT (EMQX Cloud)**: device/control event backbone.

### Cloud-Native, Docker-Ready

- Ship as container via multi-stage `Dockerfile` (builds, publishes, deploys).
- Strong configuration:
    - `appsettings.Development.json` for all connection strings, cloud endpoints, MQTT broker, etc.
    - CLI/env overrides supported.

### External Packages & Services

**C#/.NET Packages:**
- `MQTTnet` (for device integration, MQTT streaming)
- `Supabase.Client`, `Supabase.Storage`, `Supabase.Postgrest` (typed Postgres/cloud wrappers)
- `MongoDB.Driver` (native event/log storage)
- `FluentValidation` (robust, declarative DTO and schema validation)
- `Microsoft.AspNetCore` & minimal APIs approach (blazing fast endpoint composition)
- `System.Text.Json`/`System.Text.Encodings.Web` (modern, high-performance JSON)
- `ILogger` (built-in, structured logging for cloud scale)
- *and more via NuGet — see csproj for detail.*

**External Cloud Services:**
- [Supabase](https://supabase.com/) (storage, authentication, Postgres)
- [EMQX Cloud](https://www.emqx.com/en/cloud) (global MQTT broker & provisioning)
- [MongoDB Atlas](https://www.mongodb.com/cloud/atlas) (fully managed sensor/event DB)

---

## 📂 Repository & Code Structure

| Folder/File                  | Purpose                                                           |
|------------------------------|-------------------------------------------------------------------|
| `/Routes`                    | API endpoint (Minimal API groupings, e.g., TentRoutes, SensorRoutes, ImageRoutes)         |
| `/Controller.cs`             | REST controllers for input validation, business orchestration (e.g., SensorController.cs, ImageController.cs) |
| `/Services`                  | Core cloud connectors, MQTT subscriber/publisher logic, and SupabaseAuthService            |
| `/Infrastructure/Models`     | Entity/event schemas (sensor logs, tent, actuator states, etc.)    |
| `/Data`                      | Repositories (TentRepository, SupabaseSQLClient, SupabaseStorageContext) |
| `/Infrastructure/Validation` | DTO and input validators leveraging FluentValidation               |
| `appsettings.Development.json` | Cloud credentials, secrets, connection strings                       |
| `Dockerfile`                 | Dev/prod container build published in multiple stages             |
| `.dockerignore`              | Ignores build/system settings to speed up Docker context           |

---

## 🤝 How to Contribute

1. **Fork the Repository**
2. **Clone Locally**: `git clone https://github.com/Code-Syndicate-SH/Smart-Roots-Server.git`
3. **Branch**: `git checkout -b <feature-name>`
4. **Configure Env/Settings**:  
   - Duplicate `appsettings.Development.json` and fill in your cloud connection secrets (see example in repo).
   - Register/test with your own [Supabase](https://supabase.com/) and [MongoDB Atlas](https://www.mongodb.com/cloud/atlas) projects.
   - (Optionally) Use your own MQTT cloud endpoint.
5. **Install Dependencies**:  
   - Use .NET 8 SDK: `dotnet restore`
   - NuGet packages: auto-restored from `.csproj`
6. **Run Locally**:  
   - Via CLI: `dotnet run`
   - Or via Docker:  
     - `docker build -t smart-roots-server .`
     - `docker run -d -p 8080:8080 smart-roots-server`
     - *Pass env vars for cloud credentials or mount updated settings file as needed.*
7. **Write & Run Tests**
   - Add new tests under `/Tests`
8. **Open a Pull Request** and document your change!

**Contributing Guidelines:**
- Always validate new DTOs and endpoints with FluentValidation.
- Add appropriate log statements with `ILogger`.
- Ping @Code-Syndicate-SH for architectural or DB/schema changes.
- *Security*: Never hardcode/outsource secrets in PRs.

---

## 🚀 How to Run (Dev & Production)

- **Pre-Req:** .NET 8, Docker (for prod), Supabase, MongoDB, and EMQX accounts (dev keys in `appsettings.*.json`).
- **Dev:** `dotnet run`
- **Prod Container Build:**  
  - `docker build -t smart-roots-server .`
  - `docker run -d -p 8080:8080 smart-roots-server`
- **API Docs:** Minimal API, well-documented endpoints (see `/Routes` or online API docs if available).

---

## 🧩 Room for Expansion

- 🧠 AI Inference endpoints (disease, nutrient deficiency)
- 📈 Analytics API (historical logs, event queries)
- 🛡️ Per-tent/per-field RBAC (fine-grained controls)
- 🔔 Webhooks for notifications/3rd party integration
- 💬 Localization for API outputs/errors

---

## 📃 License

This repository is **not yet officially open-source licensed.**  
Contact Code-Syndicate-SH for enterprise partnership, research, or custom cloud deployment.

---

## 🔗 Reference Links

- [Smart-Roots-Server code on GitHub](https://github.com/Code-Syndicate-SH/Smart-Roots-Server)
- [See more endpoints/controllers/services (search)](https://github.com/Code-Syndicate-SH/Smart-Roots-Server/search?q=api)
- [Supabase Docs](https://supabase.com/docs)
- [EMQX Cloud](https://www.emqx.com/en/cloud)
- [MongoDB Atlas](https://www.mongodb.com/docs/atlas/)

---

> *Built with ❤️ by Code-Syndicate-SH for the future of sustainable food.*
