---
title: Dansby Development Notes
date: 2026-05-31
---

# Dansby Development Notes

## Local build and test

Run these from the repository root:

```bash
dotnet build Dansby.sln
dotnet test Dansby.sln
```

`dotnet build` proves the projects compile.

`dotnet test` builds the solution, discovers the test project, runs every test marked with `[Fact]` or `[Theory]`, and reports pass/fail results.

Before pushing code that the Ubuntu server will pull, run:

```bash
dotnet test Dansby.sln
```

## SDK selection

`global.json` keeps the repo on .NET 8 while allowing newer .NET 8 SDK feature bands:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

This lets a machine with a newer .NET 8 SDK, such as `8.0.413`, build the repo without requiring the exact original SDK patch.

## API key model

The server reads this configuration value:

```text
DANSBY_API_KEY
```

Clients send this HTTP header:

```text
X-Api-Key: <same-secret-value>
```

Protected endpoints use `ApiKeyEndpointFilter.RequireApiKey`. The filter compares the configured `DANSBY_API_KEY` with the incoming `X-Api-Key` header and returns `401 Unauthorized` if they do not match.

`GET /health` is intentionally public.

## Current request flow

The async intent path is:

```text
POST /intents
 -> IntentRequest
 -> Envelope
 -> IIntentQueue
 -> DispatcherWorker
 -> IHandlerRegistry
 -> matching IIntentHandler
 -> optional follow-up Envelope
```

`/intents` returns acceptance immediately. It does not wait for the final handler response.

Debug endpoints such as `/debug/respond` and `/debug/handle` run handlers inline and return their result directly. They are useful for development, but they are still protected by `X-Api-Key`.

## Current backend checkpoint

The first backend cleanup pass split infrastructure out of `Program.cs`:

- `Contracts/IntentRequest.cs`
- `Infrastructure/ApiKeyEndpointFilter.cs`
- `Infrastructure/InMemoryPriorityQueue.cs`
- `Infrastructure/IHandlerRegistry.cs`
- `Infrastructure/HandlerRegistry.cs`
- `Infrastructure/DispatcherWorker.cs`

The first test project is `Dansby.Tests`. It currently covers:

- priority queue behavior
- response key resolution
- response mapping contracts
- reply fallback contracts
- tokenizer behavior
- recognizer behavior

