# Example Process: Purchase Request Approval

**Status:** One example definition used to validate the generic runtime. It is not the product boundary or a claim that procurement is the target market. Validate with people who handle this process before treating it as a real customer requirement.

## Scenario

An employee submits a purchase request through an existing business application. The request is validated and routed according to a simple amount threshold. Requests above the threshold wait for a manager's decision. The requester and operational team can see the request's current state and its decision history.

The workflow coordinates the request. It does not place an order, authorize a payment, or replace a procurement or finance system.

## People and systems

- **Requester:** submits the request and needs to know its outcome.
- **Approver:** reviews requests assigned to them and approves or rejects with an optional comment.
- **Application team:** starts the workflow from an existing application and receives the final outcome.
- **Workflow runtime:** validates, routes, persists state, waits for the human decision, and records transitions.
- **Operational UI:** presents pending approvals and the request timeline.

## Main flow

1. The existing application submits a purchase request with a requester, purpose, amount, and currency.
2. The workflow validates required data and rejects invalid submissions with a clear reason.
3. A configured threshold determines the route:
   - At or below the threshold, the request is marked as approved by policy.
   - Above the threshold, the request waits for its designated manager.
4. The manager approves or rejects the request and may add a comment.
5. The workflow records the outcome and makes it available to the initiating application.
6. The requester and operational team can inspect the final state and the sequence of recorded events.

Policy-based approval in step 3 is only a demonstration rule. Real purchasing policies may require approval for every request or depend on more than amount; the rule must be configurable and must not be presented as a universal procurement policy.

## Expected outcomes

- A requester can identify whether the request is being reviewed, waiting for a manager, approved, or rejected.
- An approver can find the work assigned to them, understand the request, and record a decision.
- An application team can correlate a request with the workflow execution and obtain its outcome.
- An operator can inspect what happened and identify a request that is still waiting.

## Initial boundaries

The first scenario uses one requester, one assigned approver, one threshold rule, and one final decision. It does not include multi-level or parallel approval, delegation, escalation, purchase-order creation, payment, supplier onboarding, budget enforcement, or a procurement-system connector.

These limits keep the demonstration focused on the core problem: preserving an understandable request lifecycle across a system handoff and a human decision.

## Questions to validate

- Do teams actually lose track of purchase requests across the tools they use today?
- Who owns the request between submission, approval, and fulfillment?
- Is an amount threshold a representative routing rule, or are requester, category, department, or budget more important?
- What must the initiating application receive: a synchronous response, a callback, or a queryable final status?
- Is the operational timeline useful enough to justify a separate workflow runtime?
