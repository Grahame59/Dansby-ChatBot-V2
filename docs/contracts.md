---
title: Dansby v2.0.0 Contracts
date: 2025-09-08
---

## HTTP
- `GET /health` → `{ "status": "ok" }`
- `POST /intents` → client sends `{intent, priority?, correlationId?, payload}`; server adds `{id, ts}` and enqueues, returns `{accepted, id, correlationId}`.

## Envelope (internal)
- `id` (uuid), `ts` (UTC), `intent` (string), `priority` (0..9, default 5), `correlationId` (uuid), `payload` (JSON)

## Handler contract
`Task<HandlerResult> HandleAsync(JsonElement payload, string correlationId, CancellationToken ct)`

## Error
`{ ok:false, errorCode, message }`
