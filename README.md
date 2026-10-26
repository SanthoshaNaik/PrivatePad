# PrivatePad — Secure Encrypted Online Notepad

PrivatePad is a privacy-first, client-side encrypted online notepad. It allows users to write notes, encrypt them securely directly inside the browser using a secret passphrase, store the ciphertext in a backend database (MongoDB Atlas), and retrieve or edit them later without creating an account, registering, logging in, or installing any software.

---

## 1. Architectural Highlights

- **Client-Side Zero-Knowledge Encryption**: Notes are encrypted in the browser before transmission. Plaintext notes and user passphrases **never** leave the client browser.
- **Native Web Crypto API**: Uses the standard browser `window.crypto.subtle` API. No custom or unverified crypto algorithms.
- **Authenticated Encryption**: Uses **AES-256-GCM** with a 256-bit key and unique 96-bit IV per encryption.
- **Passphrase Key Derivation**: Uses **PBKDF2 with SHA-256** and 600,000 iterations (OWASP recommendation) with unique 128-bit salts.
- **Backend**: ASP.NET Core Web API on **.NET 10** with rate limiting, input validation, and CORS security.
- **Database**: **MongoDB Atlas** (using official `MongoDB.Driver` with automated TTL and unique index support, plus in-memory fallback for offline testing).
- **Frontend**: **Angular 22** with standalone components, Signals, and strict color palette.

---

## 2. Mandatory UI Color Palette

The interface is intentionally minimal, distraction-free, and adheres strictly to the required three-color design system:

| Purpose | Name | Hex Code |
| :--- | :--- | :--- |
| **Main Background** | Warm Ivory | `#F9EBD7` |
| **Primary Actions & Branding** | Emerald Green | `#046A38` |
| **Destructive Actions & Alerts** | Vermilion Red | `#E34222` |
| Editor Surface | White | `#FFFFFF` |
| Text & Borders | Neutral Charcoal / Tan | `#2D2825` / `#D5C7B4` |

---

## 3. Cryptographic Implementation Details

### Encryption (Client-Side)
1. **Salt & IV Generation**:
   - `salt`: 16 bytes (128 bits) cryptographically random (`crypto.getRandomValues`).
   - `iv`: 12 bytes (96 bits) cryptographically random (`crypto.getRandomValues`).
2. **Key Derivation (PBKDF2-HMAC-SHA256)**:
   - Master key material is imported from the user's passphrase as a non-extractable key.
   - Derived into a 256-bit AES-GCM key using 600,000 iterations.
   - The derived key is marked `extractable: false` so it cannot be inspected or exported from the browser's crypto context.
3. **AES-256-GCM Authenticated Encryption**:
   - Encrypts and authenticates the UTF-8 encoded note.
   - Output contains ciphertext + authentication tag.
   - Converted to Base64 for transmission to the API.

### Decryption (Client-Side)
1. Fetches ciphertext, salt, IV, and iteration count from the API.
2. Derives the AES key locally using PBKDF2-HMAC-SHA256 with the user's passphrase and stored salt.
3. Authenticates and decrypts using AES-256-GCM.
4. **Failure Handling**: If the passphrase is incorrect or any byte of ciphertext/IV has been modified, the Web Crypto API fails authentication. The application catches this safely and displays a generic error message: *"Decryption failed. Please verify your passphrase."* No sensitive internal stack traces or keys are ever shown.

### Threat Model & Limitations
- **What the server CANNOT see**: Plaintext note content, secret passphrases, derived AES keys, or unauthenticated changes.
- **What the server CAN see**: Random Note ID, encrypted payload size, creation/expiration timestamps, and client IP addresses / request metadata in network logs.
- Client-side encryption protects against server-side database breaches, but cannot protect against compromised browser environments, keyloggers, malware, or easily guessable passphrases.

---

## 4. API Endpoints

| Method | Endpoint | Description | Status Codes |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/notes` | Store new encrypted note payload | `201 Created`, `400 Bad Request` |
| `GET` | `/api/notes/{id}` | Retrieve encrypted note by Note ID | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `PUT` | `/api/notes/{id}` | Replace existing encrypted note payload | `200 OK`, `400 Bad Request`, `404 Not Found` |
| `DELETE` | `/api/notes/{id}` | Permanently delete encrypted note by ID | `204 NoContent`, `400 Bad Request`, `404 Not Found` |
| `GET` | `/api/health` | Health check and database connectivity status | `200 OK` |

### Sample Payload Format (`CreateNoteRequest` / `NoteResponse`)
```json
{
  "id": "LxUaCLR7kKuqiCUdJW",
  "ciphertext": "U2FtcGxlQ2lwaGVydGV4dERhdGE...",
  "salt": "YmluYXJ5U2FsdEhhc2gxMjM0...",
  "iv": "cmFuZG9tSVZCYXNlNjQxMjM...",
  "encryptionVersion": 1,
  "kdfIterations": 600000,
  "createdAt": "2026-10-26T12:00:00Z",
  "expiresAt": null
}
```

---

## 5. Local Setup and Running

### Prerequisites
- Node.js v20+ (Node.js 24 recommended)
- .NET 10 SDK
- MongoDB Atlas connection string (or run in default In-Memory mode)

### Backend Configuration (MongoDB Atlas)
1. Open `backend/SecureNotepad.Api/appsettings.json` (or set the environment variable `MongoDbSettings__ConnectionString`).
2. Add your MongoDB Atlas connection string:
   ```json
   "MongoDbSettings": {
     "ConnectionString": "mongodb+srv://<username>:<password>@cluster0.mongodb.net/?retryWrites=true&w=majority",
     "DatabaseName": "PrivatePadDb",
     "CollectionName": "Notes"
   }
   ```
   > **Note:** If `ConnectionString` is left empty or set to `"InMemory"`, the backend automatically runs in in-memory mode, which is ideal for testing and offline development!

3. Run the backend API:
   ```bash
   cd backend/SecureNotepad.Api
   dotnet run
   ```
   The API will listen at `http://localhost:5000` (or `https://localhost:7001`).

### Frontend Configuration (Angular 22)
1. In the repository root directory:
   ```bash
   npm install
   ```
2. Start the development server:
   ```bash
   npm start
   # or: ng serve --port 4200
   ```
3. Open your browser and navigate to:
   ```
   http://localhost:4200
   ```

---

## 6. Automated Testing

Comprehensive test suites are included for both frontend and backend.

### Frontend Tests (Vitest + Angular Testing)
Run all 23 frontend unit and integration tests:
```bash
npm test
```
**Tests Covered:**
- Component instantiation and UI layout with Warm Ivory (`#F9EBD7`), Emerald Green (`#046A38`), and Vermilion Red (`#E34222`).
- Rejection of empty notes and empty passphrases.
- Passphrase show/hide visibility toggle.
- Non-extractable Web Crypto key derivation.
- Authenticated AES-256-GCM encryption producing ciphertext without leaking plaintext.
- Independent encryptions producing different salt, IV, and ciphertext.
- Successful decryption with correct passphrase.
- Safe failure on incorrect passphrase without exposing crypto details.
- Tampered ciphertext rejection (GCM authentication tag failure).
- Complete integration lifecycle test (`encrypt` -> `POST` -> `GET` -> `decrypt` -> `PUT` -> `DELETE` -> verify `404`).

### Backend Tests (xUnit + WebApplicationFactory)
Run all 13 backend integration and API tests:
```bash
cd backend/SecureNotepad.Api.Tests
dotnet test
```
**Tests Covered:**
- `POST /api/notes`: Creates note, validates payload, returns 201 Created with high-entropy ID.
- `GET /api/notes/{id}`: Returns 200 OK with ciphertext; returns 404 for nonexistent notes.
- `PUT /api/notes/{id}`: Updates existing note; preserves Note ID.
- `DELETE /api/notes/{id}`: Deletes note (204 No Content); subsequent GET returns 404.
- Input validation: Rejection of empty ciphertext, invalid ID formats, and weak KDF iterations.
- Expired note handling (TTL eviction / 404 response).
- Health check endpoint (`GET /api/health`).
- CORS preflight headers verification for `http://localhost:4200`.

---

## 7. Project Structure

```text
PrivatePad/
├── .env.example
├── README.md
├── angular.json
├── package.json
├── tsconfig.json
│
├── src/
│   ├── index.html
│   ├── styles.css                                     # Global variables & color palette
│   ├── main.ts
│   │
│   └── app/
│       ├── app.ts                                    # Root component
│       ├── app.html
│       ├── app.css
│       ├── app.config.ts                             # HttpClient & global providers
│       ├── app.spec.ts
│       │
│       ├── core/
│       │   └── services/
│       │       └── notes.service.ts                  # Angular HttpClient API service
│       │
│       └── features/
│           └── notes/
│               ├── components/
│               │   └── note-editor/
│               │       ├── note-editor.ts            # Signal-based editor controller
│               │       ├── note-editor.html          # Minimal, accessible UI
│               │       ├── note-editor.css           # Styling with mandatory palette
│               │       └── note-editor.spec.ts       # Editor unit tests
│               │
│               ├── models/
│               │   └── note.model.ts                 # TypeScript models & DTOs
│               │
│               ├── services/
│               │   ├── encryption.service.ts         # Native Web Crypto AES-256-GCM & PBKDF2
│               │   └── encryption.service.spec.ts    # Cryptography unit tests
│               │
│               └── note-integration.spec.ts          # End-to-end frontend integration test
│
└── backend/
    ├── SecureNotepad.Api/
    │   ├── Configuration/
    │   │   └── MongoDbSettings.cs                    # Atlas configuration settings
    │   ├── Controllers/
    │   │   ├── NotesController.cs                    # REST endpoints (Create, Read, Update, Delete)
    │   │   └── HealthController.cs                   # Health status endpoint
    │   ├── DTOs/
    │   │   ├── CreateNoteRequest.cs                  # Input validation DTO
    │   │   ├── UpdateNoteRequest.cs
    │   │   └── NoteResponse.cs
    │   ├── Models/
    │   │   └── Note.cs                               # MongoDB document model
    │   ├── Services/
    │   │   ├── INoteRepository.cs                    # Storage repository abstraction
    │   │   ├── MongoNoteRepository.cs                # MongoDB.Driver Atlas implementation
    │   │   ├── InMemoryNoteRepository.cs             # Fast in-memory testing repository
    │   │   └── IdGenerator.cs                        # High-entropy random ID generator
    │   ├── Program.cs                                # Middleware, CORS, rate limiting, DI
    │   ├── appsettings.json                          # App configuration
    │   ├── appsettings.Development.json
    │   └── SecureNotepad.Api.csproj
    │
    └── SecureNotepad.Api.Tests/
        ├── NotesApiTests.cs                          # 13 xUnit integration tests
        └── SecureNotepad.Api.Tests.csproj
```


---

## 8. Cloud Deployment Guide (MongoDB Atlas + Render + Vercel)

### Architecture
- **Database**: MongoDB Atlas M0 (Free Tier)
- **Backend API**: Render Web Service (Docker container running .NET 10 ASP.NET Core)
- **Frontend**: Vercel (Angular 22 SPA)

### Quick Setup Steps
1. **MongoDB Atlas**:
   - Create a free M0 cluster at [mongodb.com/cloud/atlas](https://www.mongodb.com/cloud/atlas).
   - Under *Database Access*, create a user (e.g. `privatepad_admin`) with password.
   - Under *Network Access*, add `0.0.0.0/0` (Allow Access from Anywhere) to permit connections from Render.
   - Copy connection string: `mongodb+srv://<username>:<password>@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority`.

2. **Push Code to GitHub**:
   ```bash
   git add .
   git commit -m "Initial commit for PrivatePad"
   git remote add origin https://github.com/<YOUR_USER>/<YOUR_REPO>.git
   git push -u origin main
   ```

3. **Deploy Backend to Render**:
   - Go to [render.com](https://render.com) -> New **Web Service** -> Connect GitHub repo.
   - Select **Docker** environment. Dockerfile path: `./Dockerfile` (or `backend/Dockerfile`).
   - Add Environment Variables:
     - `ASPNETCORE_ENVIRONMENT`: `Production`
     - `MongoDbSettings__ConnectionString`: `<your_atlas_connection_string>`
     - `MongoDbSettings__DatabaseName`: `PrivatePadDb`
     - `MongoDbSettings__CollectionName`: `Notes`
   - Deploy and note your API URL: `https://<your-render-name>.onrender.com`.

4. **Deploy Frontend to Vercel**:
   - In `src/environments/environment.prod.ts`, set `apiUrl: 'https://<your-render-name>.onrender.com'`.
   - Commit & push changes.
   - Go to [vercel.com](https://vercel.com) -> Import repo -> Framework: **Angular** -> Deploy.
