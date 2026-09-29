# MVP Scope

**Status:** Proposed scope for the first usable vertical slice. It is derived from the purchase-request candidate and should be revised if user discovery invalidates that use case.

## MVP goal

Prove that an application team can start a business request in an existing application, let it wait safely for a human decision, and inspect or retrieve its outcome without relying on a spreadsheet or manual status chasing.

The MVP is a usable, self-hostable single-organization product slice—not a general-purpose process platform.

## Included

### Start and track a request

- Start a purchase request through a documented REST API.
- Validate the request and return actionable validation errors.
- Assign a stable request identifier and let the originating application query its current state and final outcome.
- Accept an idempotency key so a retried submission does not create a second request instance.

### Run the approval path

- Support one published purchase-approval workflow definition with a simple amount-threshold branch.
- Route above-threshold requests to one designated approver.
- Keep a request durably waiting for a decision across application restarts.
- Let the assigned approver approve or reject and provide an optional comment.
- Record the outcome and expose it through the request API.

### Operate and understand executions

- Show request instances in an Angular operational interface.
- Provide a pending-approval queue and request details with current state, responsible person, and chronological execution history.
- Record who made an approval decision and when.
- Make invalid, rejected, waiting, and completed requests distinguishable.

### Run locally and self-host

- Provide one documented local deployment path suitable for evaluation and a small self-hosted installation.
- Include a seeded purchase-approval example and sample API requests so a developer can start and inspect a full run.
- Keep the initial scope to one organization and one workflow definition; do not imply production readiness for multi-tenant or regulated deployments.

## MVP acceptance evidence

The MVP is successful when a reviewer can demonstrate all of the following in the running product:

1. Submit a valid request from an external client and receive an identifier that can be used to retrieve it.
2. Submit the same request again with the same idempotency key and observe the original instance rather than a duplicate.
3. Submit a request that reaches manager approval and see it in the approver's pending queue.
4. Restart the application while the request is waiting, then observe that the request is still waiting and can still be completed.
5. Approve or reject the request and see the actor, decision, timestamp, and resulting state in the execution history.
6. Retrieve the final outcome through the API and see the same outcome in the operational interface.
7. Submit a request that follows the other threshold branch and verify that it reaches the documented outcome without a manager task.

These are product acceptance conditions, not a request to add automated tests at this stage.

## Explicitly out of scope

- A drag-and-drop workflow designer or a general citizen-developer builder.
- Arbitrary workflow authoring, scripting, loops, parallel branches, or nested workflows.
- Multiple approval levels, quorum rules, delegation, escalation, SLA calendars, and reminders.
- A catalog of vendor-specific connectors or a replacement for procurement, ERP, or finance software.
- Executing a purchase, creating a purchase order, or moving money.
- AI steps, agents, chatbot features, or AI-generated workflow definitions.
- Multi-tenancy, enterprise SSO, fine-grained permission administration, and compliance certifications.
- Horizontal scaling claims, microservices, and a message broker before the single-instance lifecycle is understood.

## Product and engineering guardrails

- A waiting request is a normal durable state, not a long-running in-memory request.
- Every state change visible to the user has an understandable cause and recorded history.
- An approval decision must not be silently applied twice or accepted from an unassigned actor.
- Workflow definitions used by running instances must not change underneath those instances. The implementation approach for this guarantee belongs to the domain and architecture stages.
- Do not add a capability unless it is needed to satisfy this use case or a validated user need.

## Decisions deferred

- Whether the initiating application receives outcomes only by querying the API or also through an outbound webhook.
- How user identities and authentication are supplied by a host application versus the workflow product.
- Whether the workflow definition is represented as JSON, a typed SDK, or both.
- How failures in external actions are surfaced and retried; no external side-effect action is required to prove this MVP.
- The exact purchase policy and whether the threshold branch is representative of a real target team.
