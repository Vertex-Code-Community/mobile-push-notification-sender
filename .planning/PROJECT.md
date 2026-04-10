# PushSharp.Net

## What This Is

PushSharp.Net is a .NET 8+ library that provides a unified, simple interface for sending push notifications to mobile devices via Firebase Cloud Messaging (FCM HTTP v1) and Apple Push Notification service (APNs JWT). It is designed for server-side .NET applications — ASP.NET Core services, background workers, console apps — that need reliable push delivery without coupling to vendor-specific SDK complexity.

## Core Value

A developer can send a push notification to any device (iOS or Android) with a single `await client.SendAsync(token, notification)` call, regardless of the underlying provider.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] FCM HTTP v1 provider — OAuth2 service account authentication, send to Android/web tokens
- [ ] APNs provider — JWT (.p8 key) authentication, send to iOS/macOS device tokens
- [ ] Unified `IPushClient` abstraction — single interface for both providers
- [ ] `SendAsync(string token, PushNotification notification)` — single-token delivery
- [ ] `SendBatchAsync(IEnumerable<string> tokens, PushNotification notification)` — bulk delivery
- [ ] Microsoft.Extensions.DependencyInjection integration — `AddPushNotifications()` extension
- [ ] Provider auto-routing based on token type or explicit configuration
- [ ] Structured `PushResult` response with per-token success/failure details
- [ ] NuGet package publication-ready structure (project metadata, README, icon placeholder)

### Out of Scope

- Legacy FCM HTTP API (server key-based) — deprecated by Google, not worth implementing
- APNs certificate (.p12) auth — JWT is simpler and the modern standard; can be added later
- Web push (VAPID) — different protocol; separate concern
- .NET Framework / Xamarin / netstandard2.0 targeting — net8.0+ only to keep code clean
- Message queuing / retry infrastructure — consumers own their delivery pipeline
- Analytics or delivery tracking beyond provider response — out of library scope

## Context

- **Ecosystem**: Firebase Admin SDK and custom APNs HTTP/2 are the standard approaches in .NET. Existing libraries (PushSharp 3.x) are unmaintained and target legacy APIs. A modern, minimal library is a real gap.
- **FCM HTTP v1**: Uses OAuth2 tokens from a Google service account JSON file. Tokens expire every hour and must be refreshed — library must handle this transparently.
- **APNs**: Uses HTTP/2 with JWT tokens signed by a .p8 private key. JWT tokens expire every hour. Connection pooling matters for throughput.
- **Target consumers**: Any .NET 8+ app. Primary use case is an ASP.NET Core backend dispatching notifications triggered by business events.

## Constraints

- **Platform**: .NET 8.0+ only — no netstandard, no legacy targets
- **Dependencies**: Minimize third-party deps; prefer `System.Net.Http` + `System.Text.Json`; use Google Auth Library only for FCM OAuth2 token fetching
- **Auth**: FCM via service account JSON (Google.Apis.Auth); APNs via .p8 + BouncyCastle or built-in ECDsa for JWT signing
- **HTTP/2**: APNs requires HTTP/2 — use `HttpClient` with `SocketsHttpHandler` and `EnableMultipleHttp2Connections`

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| FCM HTTP v1 only (no legacy) | Legacy API is deprecated by Google; building on it wastes effort | — Pending |
| APNs JWT only (no .p12 cert) | JWT is simpler to distribute (no keychain), more modern | — Pending |
| net8.0 only | Avoids compatibility shims, enables modern C# features | — Pending |
| Simple method call API (not fluent builder) | Lower cognitive load for common case; fluent can be added later | — Pending |
| DI-first with `AddPushNotifications()` | ASP.NET Core is the primary consumer; DI is standard there | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-04-10 after initialization*
