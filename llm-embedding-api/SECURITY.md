# Security — Authentication & Rate Limiting

This document covers how the LLM Embedding API authenticates callers and rate-limits
traffic. It replaces the previous hard-coded `x-api-key` check in `Program.cs`.

## Route inventory

| Route | Auth required? | Rate limited? |
|-------|-----------------|----------------|
| `GET /` | No | No |
| `GET /health` | No | No |
| `GET /swagger` (development only) | No | No |
| `GET /api/model/status` | **No** (explicit exception — see below) | No |
| `POST /api/embed` | Yes | Yes — `api` policy |
| `POST /api/embed/batch` | Yes | Yes — `batch` policy (stricter) |
| `GET /api/models` | Yes | Yes — `api` policy |
| `POST /api/model/download` | Yes | Yes — `api` policy |
| `GET /api/metrics/current` | Yes | Yes — `api` policy |
| `GET /api/metrics/statistics` | Yes | Yes — `api` policy |
| `GET /api/metrics/snapshots` | Yes | Yes — `api` policy |
| `POST /api/metrics/gc` | Yes | Yes — `api` policy |

`GET /api/model/status` is intentionally anonymous — it only reports whether the model
file exists/is valid on disk, no sensitive data, and is used as a secondary readiness
signal alongside `/health`. The exception is a hard-coded allow-list in
`Middleware/ApiKeyAuthMiddleware.cs` (`AnonymousPaths`), not something configurable —
if you add new low-sensitivity endpoints under `/api/*` that should stay anonymous, add
them there deliberately.

## Authentication

Callers present a key via either header (checked in this order):

1. `x-api-key: <key>` (kept for backwards compatibility with existing clients)
2. `Authorization: Bearer <key>` (fallback)

The key is validated by `Services/Auth/ApiKeyValidator.cs` against two possible sources,
checked in order:

### 1. Static keys (`Auth:ApiKeys`)

Configured as a list of SHA-256 **hashes** — the plaintext key is never stored in
configuration, source control, or logs:

```json
"Auth": {
  "ApiKeys": [
    { "KeyHash": "<sha256-hex>", "Name": "some-consumer", "TenantId": "optional-tenant-id" }
  ]
}
```

- `KeyHash` — required. Lowercase hex-encoded SHA-256 hash of the plaintext key.
- `Name` — optional, for logging/auditing only.
- `TenantId` — optional. If set, the request is treated as scoped to that tenant.

Generate a hash with the snippet in [`tools/hash-key`](tools/hash-key):

```bash
# bash / macOS / Linux
./tools/hash-key/hash-key.sh
# prompts for the key (hidden input), prints the SHA-256 hex hash

# PowerShell / Windows
./tools/hash-key/hash-key.ps1
```

Put the **hash** in config (env var, Key Vault, or `Auth__ApiKeys__0__KeyHash`) —
never the plaintext key. Distribute the plaintext key to the consuming service out of
band; it cannot be recovered from the hash.

### 2. Remote validation (`Auth:RemoteValidation`) — optional

Delegates validation to the organization service instead of (or in addition to) static
keys. Static keys are always checked first; remote validation only runs if none match.

```json
"Auth": {
  "RemoteValidation": {
    "Enabled": false,
    "Endpoint": "https://organization.internal/api/org/apikeys/validate",
    "TimeoutSeconds": 5,
    "PositiveCacheMinutes": 5,
    "NegativeCacheMinutes": 1
  }
}
```

Contract (as of Phase 6 — the organization service endpoint is being built in parallel;
this implementation is coded against the contract and needs an integration check once
that endpoint is live):

- Request: `POST {Endpoint}` with body `{"apiKey": "<plaintext key>"}`
- Response `200 OK` with body `{"tenantId": "...", "scopes": ["..."], "status": "Active"}`
  is the **only** combination treated as valid.
- Any non-200 response, or a 200 with `status` other than `"Active"`, is treated as
  invalid.
- Network errors / timeouts are treated as a **transient** failure (HTTP 503 returned to
  the caller) rather than an invalid key, and are *not* written to the negative cache —
  so an org-service outage doesn't lock out legitimate keys for the negative-cache TTL.

Results are cached in-memory (per process) to avoid a round trip per request:
- Valid ("Active") results: cached for `PositiveCacheMinutes` (default 5).
- Invalid results (non-200, or status != Active): cached for `NegativeCacheMinutes`
  (default 1) — kept short so a key that's activated shortly after a failed check isn't
  locked out for long.

### Failure responses

| Condition | Status |
|-----------|--------|
| No key presented | 401 |
| Key doesn't match any static hash and isn't valid per remote validation | 403 |
| Remote validation enabled but temporarily unreachable/timed out | 503 |

## Rate limiting

Requests are rate-limited per API key (SHA-256 hash of the raw key value, so the
plaintext key is never retained as a cache/partition key), falling back to per-client-IP
for requests with no key. Configured via:

```json
"RateLimiting": {
  "PermitPerMinute": 120,
  "BatchPermitPerMinute": 30,
  "QueueLimit": 0
}
```

- `PermitPerMinute` (default 120) — general `/api/*` endpoints (`"api"` policy): embed,
  models, model/download, and all `/api/metrics/*` endpoints.
- `BatchPermitPerMinute` (default 30) — `POST /api/embed/batch` only (`"batch"` policy),
  stricter because batch requests are more expensive.
- `QueueLimit` (default 0) — requests allowed to queue once the limit is hit, before
  being rejected with `429 Too Many Requests`.

Rate limiting runs *before* authentication in the pipeline, so invalid-key/brute-force
traffic is throttled too, not just authenticated traffic.

`GET /health` and `GET /api/model/status` are not rate-limited.

## Config keys reference

| Key | Default | Notes |
|-----|---------|-------|
| `Auth:ApiKeys:*:KeyHash` | — | Required per entry. SHA-256 hex hash of a static key. |
| `Auth:ApiKeys:*:Name` | — | Optional label for logging. |
| `Auth:ApiKeys:*:TenantId` | — | Optional tenant scope. |
| `Auth:RemoteValidation:Enabled` | `false` | Enables delegating to the org service. |
| `Auth:RemoteValidation:Endpoint` | — | Org service validation URL. |
| `Auth:RemoteValidation:TimeoutSeconds` | `5` | HTTP timeout for the validation call. |
| `Auth:RemoteValidation:PositiveCacheMinutes` | `5` | TTL for cached valid results. |
| `Auth:RemoteValidation:NegativeCacheMinutes` | `1` | TTL for cached invalid results. |
| `RateLimiting:PermitPerMinute` | `120` | General `/api/*` limit, per partition. |
| `RateLimiting:BatchPermitPerMinute` | `30` | `/api/embed/batch` limit, per partition. |
| `RateLimiting:QueueLimit` | `0` | Requests queued before 429s start. |

All of the above can be set via `appsettings.{Environment}.json`, environment variables
(double-underscore notation, e.g. `Auth__ApiKeys__0__KeyHash`), Docker Compose env vars
(see `docker-compose.yml` / `.env.example`), or ACI/Key Vault (see `infra/main.bicep`).

**By default, `Auth:ApiKeys` is empty and remote validation is disabled** — every
`/api/*` request (other than the anonymous exceptions above) is rejected until you
configure at least one key hash or enable remote validation. This is intentional
(secure-by-default) rather than an oversight.

## Known issue found during this hardening pass

`infra/main.parameters.json` had a **plaintext Azure Storage account key committed to
source control** (`storageAccountKey`). It has been replaced with a placeholder in this
change, but the key was already exposed in git history before this change and should be
**rotated in Azure immediately** regardless. `infra/Deploy-ACI.ps1` already supports
overriding it at deploy time via `-StorageAccountKey` or the `ACI_STORAGE_ACCOUNT_KEY`
environment variable, so the checked-in value should never need to be real.
