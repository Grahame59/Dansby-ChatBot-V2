---
title: Dansby v2 Architecture (MVP)
date: 2025-09-08
---

Flow: `POST /intents` → in-memory priority queue → background worker → handler registry → handler runs and logs result.

Future: pull handlers into separate processes, add MQTT, scheduler, health checks.
