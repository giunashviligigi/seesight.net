# 0002. AI Service never calls back into Trip Service — shortlist is always supplied inline

Status: Accepted
Date: 2026-08-03

## Context

The first documentation pass had two synchronous REST edges between the same two services in opposite directions: Trip Service → AI Service ("request a recommendation") and AI Service → Trip Service ("fetch the trip's persisted offer snapshots when the caller didn't inline them"). A caller-then-callee edge that loops back to the original caller is a circular synchronous dependency — it makes failure modes harder to reason about (a slow/unavailable Trip Service can now stall a request Trip Service itself initiated), complicates the service dependency graph for no functional benefit, and is unnecessary here specifically because **the only realistic caller of AI Service already has the offer data before it calls AI Service** — either the frontend just fetched the trip detail (which includes its offer snapshots) from Trip Service to render the page the user is looking at, or Trip Service itself has the data in hand as part of whatever workflow triggered the recommendation request.

## Decision

`IAIService.RecommendItinerary` always receives the offer shortlist **inline in the request payload**. AI Service never makes an outbound call to Trip Service (or any other service) to fetch it, and — resolving the "which direction" ambiguity the first draft left open — **Trip Service does not call AI Service either**. The only caller of AI Service is the Gateway, routing the frontend's request straight through. This matches the original system's actual behavior most faithfully (the client calls the recommendation endpoint directly, passing or having the service look up whatever shortlist it needs) and makes AI Service's only outbound dependency the Groq API — no service-to-service REST dependency at all, synchronous or otherwise, in either direction.

`POST /ai/recommend-itinerary`'s request DTO includes the flight/hotel shortlist (ids + the fields the model needs to reason about, e.g. price/duration/stops) as a required field, not an optional one with a server-side fallback fetch. The frontend already holds this data from its own prior calls to Trip Service (to render the trip page) and/or Search Service (to render search results), so supplying it inline costs nothing extra.

## Consequences

- AI Service becomes trivially easy to reason about, test, and scale independently — it has no upstream service dependency to mock beyond Groq itself, and no service depends on it synchronously either (only the Gateway routes to it).
- The circular Trip Service ↔ AI Service edge is eliminated from the service dependency graph entirely — there is no edge between these two services at all now, in either direction (see the corrected diagrams in [Microservices.md](../Microservices.md) §3–§5).
- The frontend is responsible for including the shortlist in its request — a small increase in caller-side responsibility, judged clearly worth it for removing a circular dependency and matching the original system's design. If a genuine server-initiated recommendation need emerges later (e.g. an automated pre-trip suggestion with no user request in the loop), that would be a new, explicitly-designed edge added then — not built speculatively now.
- `AiRecommendation` persistence (§ [DatabaseDesign.md](../DatabaseDesign.md) §6) is unaffected — AI Service still stores what it recommended and why, keyed by `TripId`, purely as its own write, not as a read-back dependency on Trip Service.
