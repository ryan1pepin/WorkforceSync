# WorkforceSync — Panel Demo Script

> ~8–10 minute live walkthrough, built to land the "integration + queuing + full-stack"
> story. Run `scripts/demo.ps1` first so everything is up and the DB is fresh.
> Keep the browser on the dashboard; have Swagger open in a second tab as backup.

---

## 0. Before you start (30 seconds)

- `.\scripts\demo.ps1` — fresh DB, API + feed + UI all running, browser opens.
- Register a quick account on the login screen (any email / password).
- Confirm the dashboard shows **3 seeded employees** and the pipeline is **Healthy**.
- Have Swagger (`/swagger`) open in a second tab — it's your fallback if the UI hiccups.

---

## 1. The one-line pitch (say this first)

> "This is a working HR data-integration platform. It mirrors the exact pattern I use
> in production — an Oracle HCM Cloud source publishing ATOM feeds, a background poller
> that ingests them, a bounded in-memory queue with backpressure, an idempotent
> processor that transforms and persists, and a JWT-secured REST API feeding a live
> Angular dashboard. I built it end-to-end so I could show the whole pipeline, not just
> a slide."

**Why this lands:** it names the production pattern (Oracle HCM ↔ enterprise systems)
before showing anything, so the panel maps what they see to what they already run.

---

## 2. Walk the architecture (point, don't read)

Point at the diagram in the README (or just narrate):

> "The mock HCM on the left is standing in for Oracle HCM Cloud. It publishes an ATOM
> feed of workforce events — hires, position changes, comp changes, terminations. The
> API has a background poller that fetches that feed on an interval, enqueues each new
> event into a bounded `Channel<T>`, and a separate processor drains it — transform,
> validate, upsert the employee and position, and write an audit row. The whole thing
> is idempotent, so a redelivered event is a no-op."

**Key phrase to say:** *"The poller and the processor are decoupled by the queue —
that's the same shape as a Service Bus topic, just in-process."*

---

## 3. Show it live (the money shot)

> "Watch the dashboard. Right now there are three employees. The mock HCM is going to
> publish a new event every 15 seconds — I don't have to do anything. Watch the audit
> log at the bottom and the employee table above it."

Then **wait**. Let 2–3 events land on their own. Narrate as they appear:

- **Position change** → "Ada just got promoted to Senior Software Engineer — the title
  updated, and you can see the audit row for it."
- **Comp change** → "Grace's base salary just moved — same employee, new comp, one row
  in the audit log."
- **Termination** → "Alan's status just flipped to Terminated. Note he's still in the
  table — we don't delete people, we mark them inactive. That's how HR data should
  behave."
- **New hire** → "And a brand-new employee just appeared — Katherine, Flight Engineer.
  That was a full create, not an update."

**Why this lands:** you're not clicking a button to make data appear. The system is
*ingesting a live feed* and the UI is *reacting*. That's the whole integration story
in 60 seconds, with zero manual steps.

---

## 4. Prove the security (30 seconds)

> "Everything behind that dashboard is JWT-secured. The access token is short-lived;
> when it expires, the Angular interceptor transparently calls the refresh endpoint,
> rotates the pair, and retries the request — the user never sees a 401. And the
> refresh token is single-use with reuse detection, so a stolen refresh token gets
> you locked out, not in."

(If they want proof: open Swagger, hit `/employees` with no token → 401. With a token → 200.)

---

## 5. The "why I built it this way" close (the differentiator)

> "Three things I'd call out as deliberate engineering choices:
>
> **One — idempotency.** Every event has a stable ID and I record what I've already
> applied. If the feed redelivers, or I restart the service, nothing double-applies.
> In HR data, a double-applied comp change is a real incident, so I designed for it.
>
> **Two — backpressure.** The queue is bounded. If the processor falls behind, the
> poller waits instead of buffering unbounded memory. That's the difference between a
> demo and something that survives a bad day in production.
>
> **Three — isolation of failure.** One bad event gets logged to the audit trail and
> the pipeline keeps going. A single malformed record doesn't wedge the whole feed."

**Then hand it back:** "Happy to go deeper on any of those — the queue, the idempotency
key, the token rotation — whichever is most useful to the team."

---

## 6. Likely follow-ups (have these ready)

| Question | Short answer |
|---|---|
| "Why in-process queue and not a real broker?" | "Scope of a portfolio build — but the boundary is identical to a Service Bus topic. Swapping `Channel<T>` for a broker is a transport change, not an architecture change." |
| "What if the feed is down?" | "The poller logs and retries on the next interval. Transient failures are expected and handled; the pipeline never crashes on a missed poll." |
| "How do you handle a bad event?" | "It's isolated to the audit log with a failure status. The processor moves on. Nothing wedges." |
| "Is the data real?" | "The source is a scripted mock of Oracle HCM's ATOM feed — deterministic, so the demo is repeatable. The ingestion, transform, queue, and persistence are all real." |
| "What would you change for production?" | "Real broker, migrations instead of `EnsureCreated`, structured logging + metrics, and a dead-letter path for poison events." |

---

## 7. If something breaks (don't panic)

- **UI blank / not loading** → Swagger is your fallback. Hit `/employees` and `/integrations/health` and narrate the JSON. The pipeline is still the story.
- **No events appearing** → the feed is on `:5199/feed`. `curl` it to show the ATOM XML is live, then point back at the dashboard.
- **401 everywhere** → re-login. The token may have expired mid-demo; the interceptor should have handled it, but a fresh sign-in is a 5-second fix.

**Rule:** the *pipeline* is the demo, not the pixels. If the UI fails, the API + feed
still prove the integration story. Never let a UI glitch become the story.
