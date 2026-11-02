# EventHive — Functional Requirements Specification

**Microservices learning project — ASP.NET Core**

---

## Document Scope

This document lists functional requirements only. Non-functional requirements (resilience, observability, scalability, security hardening, etc.) are covered separately. The goal of EventHive is to practice service-to-service integration patterns — API gateway routing, service discovery, third-party authentication, synchronous and asynchronous communication, and payment provider integration — using a deliberately small data model.

---

## Actors

- **Guest:** An unauthenticated visitor who can browse public event listings.
- **Registered User:** A guest who has authenticated via Google and can book and pay for events.
- **System / Scheduler:** Internal automated processes (e.g. expiring stale pending bookings).
- **Payment Provider (Paymob):** External system that initiates and confirms payments via webhook.
- **Admin:** An internal/back-office user who manages the event catalog.

---

## Module Overview

| Module | Service(s) | Primary Responsibility | Access |
| :--- | :--- | :--- | :--- |
| **API Gateway, Service Discovery** | API Gateway | Single entry point for clients; dynamic routing to healthy service instances | Public |
| **Identity** | Identity Service | External login via Google, internal JWT issuance and validation | Public / Protected |
| **Catalog** | Catalog Service | Source of truth for events, seats/capacity, and admin event management | Public / Admin |
| **Booking** | Booking Service | Orchestrates the booking lifecycle; bridges sync and async calls | Protected |
| **Payment** | Payment Service | Integrates with Paymob; publishes payment outcomes | Protected / Webhook |
| **Notification** | Notification Service | Consumes events and informs the user of status changes | Internal (Consumer) |

---

## 1. API Gateway & Service Discovery

The gateway is the single entry point for every client request. Service discovery lets services register and be located without hardcoded addresses, so the set of running instances can change without reconfiguring the gateway.

### FR-GTW-01: Central Request Routing
The API Gateway shall route every incoming client request to the correct downstream service based on the request path.
- Each downstream service is reachable through a distinct route prefix (e.g. `/identity/*`, `/catalog/*`, `/bookings/*`, `/payments/*`).
- The gateway shall return a 404/consistent error response for unmatched routes rather than forwarding them blindly.

### FR-GTW-02: Dynamic Service Resolution
The API Gateway shall resolve the physical address of a downstream service at request time by querying the service discovery registry, instead of using a statically configured address.
- If a service has multiple healthy instances registered, the gateway shall distribute requests across them (round-robin is sufficient).
- If no healthy instance is registered for a requested service, the gateway shall return a clear service-unavailable response.

### FR-GTW-03: Service Registration
Every downstream microservice shall register itself with the service discovery registry on startup and deregister (or be marked unhealthy) on shutdown or health-check failure.
- Registration includes service name, host, port, and a health-check endpoint.
- The registry shall periodically probe each registered instance's health-check endpoint.

### FR-GTW-04: Centralized Authentication Enforcement
The API Gateway shall validate the internal JWT on every route that requires authentication before forwarding the request downstream.
- Public routes (e.g. browsing events, login) are explicitly whitelisted and bypass token validation.
- Requests with a missing, malformed, or expired token to a protected route are rejected at the gateway and never reach the downstream service.
- The gateway shall forward the authenticated user's identity (e.g. user id, roles) to downstream services via request headers or claims.

### FR-GTW-05: Request Aggregation Entry Point (Optional / Stretch)
The API Gateway may expose a single composite endpoint that returns a user's upcoming bookings together with basic event details, to demonstrate a gateway-level aggregation call.
- This aggregation calls the Booking Service and Catalog Service and merges the results before responding to the client.

---

## 2. Identity & Authentication Service

Authentication is delegated to Google as the external identity provider. The Identity Service exchanges a validated Google identity for an internal, service-trusted JWT used across the system.

### FR-IDN-01: Sign-in with Google
A Guest shall be able to initiate authentication using their Google account through an OAuth2/OpenID Connect flow.
- The system redirects the user to Google's consent screen and receives an authorization code/ID token back.
- The Identity Service validates the Google ID token's signature, issuer, audience, and expiry before trusting it.

### FR-IDN-02: Local User Provisioning
On a user's first successful Google sign-in, the Identity Service shall create a local user record (id, email, display name, Google subject id).
- On subsequent sign-ins, the existing local user record is matched by the Google subject id rather than creating a duplicate.

### FR-IDN-03: Internal Token Issuance
After a successful Google sign-in, the Identity Service shall issue its own signed JWT (access token) representing the user within EventHive.
- The token includes user id, email, role(s), issuer, audience, and an expiry claim.
- The token is signed with a key/algorithm that every other service can verify independently (or via the gateway).

### FR-IDN-04: Token Refresh
A Registered User shall be able to obtain a new access token without repeating the full Google login, as long as their session/refresh token is still valid.
- Expired refresh tokens require a full re-authentication via Google.

### FR-IDN-05: Current-User Profile Lookup
A Registered User shall be able to retrieve their own basic profile (id, name, email) using a valid access token.

### FR-IDN-06: Sign-out
A Registered User shall be able to invalidate their current session (revoke/blacklist the refresh token) so it can no longer be used to mint new access tokens.

---

## 3. Catalog Service

The Catalog Service is intentionally small: its main purpose is to give the Booking Service a real synchronous dependency, not to be a full content-management system. It now includes a lightweight admin surface (section 3.1) so events can be managed at runtime instead of relying purely on seed data.

### FR-CAT-01: List Published Events
A Guest or Registered User shall be able to retrieve a list of published events, including name, date/time, venue, price, and remaining seat count.
- Only events marked as published are returned to end users.

### FR-CAT-02: Get Event Details
A Guest or Registered User shall be able to retrieve full details for a single event by its id.
- Requesting a non-existent or unpublished event id returns a not-found response.

### FR-CAT-03: Seat / Capacity Decrement
The Catalog Service shall expose an internal endpoint the Booking Service calls to provisionally reserve (decrement) seats once a booking request passes the availability check.
- If a booking is later cancelled or expires, the reserved seats shall be released back to the event's available count (via an async event or a compensating sync call).

### FR-CAT-04: Seat / Capacity Increment
The Catalog Service shall expose an internal endpoint the Booking Service calls to provisionally release (increment) seats.

### 3.1 Event Management (Admin)
Rather than relying on seed data alone, the Catalog Service shall provide a small admin-only surface so events can be created and maintained at runtime. This remains the only CRUD-heavy area of the system by design — depth here is about lifecycle control (draft/publish/close), not a full back-office suite.

### FR-CAT-05: Create an Event
An Admin shall be able to create a new event by providing name, description, date/time, venue, price, and total capacity.
- A newly created event defaults to Draft status and is not visible to Guests or Registered Users until published (FR-CAT-08).
- Total capacity must be a positive integer; remaining seats is initialized to the total capacity.
- The system validates that the event date/time is in the future.

### FR-CAT-06: Update Event Details
An Admin shall be able to update an existing event's name, description, date/time, venue, and price.
- Editing an event that already has confirmed bookings triggers a warning but is still permitted (e.g. venue or time changes); the system does not auto-notify booked users in this phase.
- Total capacity can be increased at any time; decreasing it below the number of seats already reserved/sold is rejected.

### FR-CAT-07: List All Events (Admin View)
An Admin shall be able to retrieve a list of all events regardless of status (Draft, Published, Closed, Cancelled), including remaining capacity and count of bookings.
- This view is distinct from the public listing (FR-CAT-01), which only returns Published events.

### FR-CAT-08: Publish an Event
An Admin shall be able to transition an event from Draft to Published, making it visible and bookable through the public endpoints.
- An event cannot be published without a name, date/time, price, and capacity already set.

### FR-CAT-09: Unpublish / Close an Event
An Admin shall be able to transition a Published event to Closed, immediately preventing any new bookings while leaving existing bookings unaffected.
- A Closed event no longer appears in the public listing (FR-CAT-01) but remains retrievable by direct id for users with an existing booking.

### FR-CAT-10: Cancel an Event
An Admin shall be able to cancel an event entirely (e.g. the venue fell through), marking it Cancelled.
- Cancelling an event publishes an `EventCancelled` message to RabbitMQ so the Booking Service can cancel all associated bookings and the Notification Service can inform affected users.

### FR-CAT-11: Manual Capacity Adjustment
An Admin shall be able to manually adjust an event's remaining seat count (e.g. to hold back seats for offline sales or release held seats back for sale).
- A manual adjustment cannot reduce remaining seats below zero or above the event's total capacity.
- Every adjustment is recorded with a reason, for traceability against automatic increments/decrements made by the Booking Service (FR-CAT-03, FR-CAT-04).

---

## 4. Booking Service

The Booking Service is the orchestrator: it ties the synchronous availability check to the asynchronous payment flow and owns the booking's lifecycle state.

### FR-BOK-01: Create a Booking Request
A Registered User shall be able to request a booking for a specific event and seat quantity.
- The Booking Service synchronously calls the Catalog Service's availability check (FR-CAT-03) before creating anything.
- If seats are unavailable, the request is rejected immediately with no booking record created.
- If seats are available, the Booking Service creates a booking in `Pending` status and calls the Catalog Service to reserve the seats (FR-CAT-04).

### FR-BOK-02: Publish Booking-Created Event
After creating a `Pending` booking, the Booking Service shall publish a `BookingCreated` message to RabbitMQ containing booking id, user id, event id, quantity, and amount due.

### FR-BOK-03: Consume Payment Outcome Events
The Booking Service shall consume `PaymentSucceeded` and `PaymentFailed` messages from RabbitMQ and update the corresponding booking's status accordingly (see status table below).
- Consumption is idempotent: receiving the same payment outcome message twice must not double-apply the state change.
- An outcome message referencing an unknown booking id is logged and safely discarded (never crashes the consumer).

### FR-BOK-04: View My Bookings
A Registered User shall be able to list their own bookings with current status, event name, quantity, and amount.

### FR-BOK-05: View Booking Details
A Registered User shall be able to retrieve the full detail of a single booking they own, including its current status and payment reference (if any).
- A user requesting another user's booking receives a not-found/forbidden response.

### FR-BOK-06: Cancel a Pending Booking
A Registered User shall be able to cancel a booking that is still in `Pending` status before payment completes.
- Cancelling releases the reserved seats back to the Catalog Service.
- A booking that is already `Confirmed` cannot be cancelled through this flow (out of scope for this phase).

### FR-BOK-07: Expire Stale Pending Bookings
The system shall automatically move a `Pending` booking to `Expired` if no payment outcome is received within a configured hold window (e.g. 15 minutes).
- Expiring a booking releases its reserved seats back to the Catalog Service, the same as a manual cancellation.

### Booking Status Lifecycle

| Status | Meaning | Triggered By |
| :--- | :--- | :--- |
| **`Pending`** | Booking created, seats provisionally held, awaiting payment outcome | `BookingCreated` published |
| **`Confirmed`** | Payment succeeded | `PaymentSucceeded` consumed |
| **`Cancelled`** | Payment failed, payment timed out, or user cancelled before payment | `PaymentFailed` consumed / timeout / user action |
| **`Expired`** | No payment event received within the configured hold window | Scheduled check / TTL on pending booking (15 min) |

---

## 5. Payment Service

The Payment Service is the boundary between EventHive and the outside world's money movement. It is driven entirely by the async `BookingCreated` event and a webhook callback from Paymob.

### FR-PAY-01: Consume Booking-Created Event
The Payment Service shall consume `BookingCreated` messages from RabbitMQ and initiate a payment with Paymob for the booking's amount.
- Consumption is idempotent: the same `BookingCreated` message received twice must not create two separate payment intents with Paymob.

### FR-PAY-02: Create a Paymob Payment Intent / Checkout Link
For each new `BookingCreated` message, the Payment Service shall call Paymob's API to create a payment order and obtain a hosted checkout URL or iframe token.
- The Payment Service stores a local payment record linking booking id, Paymob order id/reference, amount, and status (`Initiated`).

### FR-PAY-03: Expose Checkout Link to the User
A Registered User shall be able to retrieve the checkout URL/token for their pending booking's payment, to complete payment on Paymob's hosted page.

### FR-PAY-04: Handle Paymob Webhook Callback
The Payment Service shall expose an endpoint that receives Paymob's transaction webhook (success or failure) and verifies its authenticity (HMAC/signature check) before trusting the payload.
- An invalid or unverifiable signature is rejected and never treated as a real payment outcome.

### FR-PAY-05: Publish Payment Outcome Event
After validating the webhook, the Payment Service shall update its local payment record and publish a `PaymentSucceeded` or `PaymentFailed` message to RabbitMQ, correlated to the original booking id.
- This is the message the Booking Service consumes per FR-BOK-03.

### FR-PAY-06: Duplicate Webhook Protection
The Payment Service shall recognize a duplicate webhook call for a transaction it has already finalized and avoid publishing a second outcome event for it.

### FR-PAY-07: View Payment Status
A Registered User shall be able to check the current status of the payment tied to one of their bookings (`Initiated`, `Succeeded`, `Failed`).

---

## 6. Notification Service

A deliberately simple consumer-only service. It exists to demonstrate a second, independent consumer of the same async events, and can start as just structured logging or a mocked email.

### FR-NOT-01: Notify on Booking Confirmation
The Notification Service shall consume `PaymentSucceeded` messages and send the user a booking-confirmation notification including event name, date, and quantity.

### FR-NOT-02: Notify on Payment Failure
The Notification Service shall consume `PaymentFailed` messages and notify the user that their booking could not be completed, inviting them to retry.

### FR-NOT-03: Notify on Booking Expiry
The Notification Service shall consume a `BookingExpired` message (published by the Booking Service per FR-BOK-07) and inform the user their held seats were released.
- This requires the Booking Service to also publish a `BookingExpired` event, in addition to updating its own state.

### FR-NOT-04: Notification Idempotency
The Notification Service shall avoid sending duplicate notifications for the same event message if it is redelivered by RabbitMQ.

### FR-NOT-05: Notify on Event Cancellation
The Notification Service shall consume `EventCancelled` messages (published per FR-CAT-10) and inform every user with a booking on that event that it was cancelled and their booking voided.

---

## 7. Cross-Cutting Messaging Requirements

These apply to every producer/consumer pair in the system and tie the modules above into one coherent flow.

### FR-MSG-01: Defined Event Contracts
Every message exchanged over RabbitMQ (`BookingCreated`, `PaymentSucceeded`, `PaymentFailed`, `BookingExpired`, `EventCancelled`) shall follow a versioned, documented schema shared by producer and consumers.

### FR-MSG-02: At-Least-Once Delivery Assumption
Every message consumer in the system (Payment Service, Booking Service, Notification Service) shall be designed as if messages can be delivered more than once, and shall handle duplicates safely (see the idempotency requirements in each module above).
