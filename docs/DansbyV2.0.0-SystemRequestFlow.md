---
title: Dansby v2.0.0 How a Request Flows through the System (currently)
date: 2025-09-14
---

How a request flows through your system

### 1. Client sends a message
    Example:

    ``` bash

        curl -s -X POST http://<server-ip>:8087/intents \
            -H 'Content-Type: application/json' \
            -H 'X-Api-Key: superlongrandomvalue123' \
            -d '{"intent":"nlp.recognize","priority":5,"payload":{"text":"dansby turn on the living room light"}}'
    ``` 
### 2. /intents endpoint (Program.cs)

    - Checks the API key
    - Validates the intent name exists
    - Normalizes data and builds an Envelope
    - Enqueues the Envelope onto the priority queue
    - Returns immediately (202/200) to the client

    It's not processing the work here — its just framing it and pushing it onto the queue.

### 3. DispatcherWorker (BackgroundService)

    - Loops, pops an Envelope off the queue
    - Finds the right handler by the envelope’s Intent via HandlerRegistry
    - Calls the handler’s HandleAsync(payload, correlationId, ct)
    - Logs success or error

### 4. A handler (your “small pipe”)

    - Only cares about its intent’s payload shape
    - Does work, returns HandlerResult (Ok/Data or Error)
    - Example handlers: nlp.recognize, iot.lights.set, sys.time.now, etc.
    
### So for intent recognition specifically:

    - The client sends /intents with intent: "nlp.recognize" and payload.text.
    - The dispatcher runs the NlpRecognizeHandler, which calls your v1.1 recognizer.
    - The handler returns { intent, score, domain, slots } as Data (and you see it logged).
    Later we can auto-route to iot.lights.set, but first we just want recognition working.

    ---
