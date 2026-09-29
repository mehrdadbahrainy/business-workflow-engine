# Product Discovery Plan

**Status:** Planned; no target-user interviews have been conducted for this repository yet.

## Decision this research should support

Determine whether a developer-integrated, self-hosted workflow runtime solves a recurring and costly coordination problem for a reachable group of application teams and operational users. This is problem discovery, not a product-market-fit claim or a usability study of the current implementation.

Until evidence changes the current assumptions, keep purchase approval as a bounded demonstration and avoid adding general workflow authoring, connector catalogs, or AI capabilities.

## Hypotheses to examine

1. **Lifecycle visibility:** recurring requests lose clear ownership or status when they cross people and business applications.
2. **Application-team fit:** application teams need to coordinate those requests and find their existing application logic, workflow products, or manual handoffs insufficient for a specific reason.
3. **Operational fit:** requesters, approvers, and operators need one durable place to act on assigned work and understand how it reached an outcome.
4. **Deployment fit:** self-hosting, data control, or integration ownership is a meaningful requirement for the intended audience, rather than an assumption based on the project author's preferences.
5. **First-use-case fit:** purchase approval resembles a real recurring process and exposes the right workflow primitives without taking on procurement or finance policy.

## Participants

For an initial discovery round, seek 6–8 people who have personally handled a recent cross-application business request:

- At least three application or integration engineers who own internal or line-of-business software.
- At least three operational users or approvers who submit, route, review, or follow up on those requests.
- Include the person accountable for the process when that is a different role.

Do not treat this small sample as statistically representative. Keep industry, organization size, and existing workflow tooling open until repeated evidence supports narrowing the audience.

## Interview format

Use a 30–45 minute conversation. Spend the first part on the most recent real example before describing this project or showing its interface. Ask about observed behavior and consequences; do not ask participants to validate a proposed feature list.

1. Tell me about the most recent request that began in one application and needed work from another person or system.
2. What started it, who touched it, and which systems or manual channels were involved?
3. How did each person know who owned the next step and whether the request was still active?
4. What happened the last time information was missing, a decision was delayed, or the request did not reach the expected outcome?
5. How often does this happen? What time, rework, delay, or risk did the last example create?
6. What tools or process changes have you tried? What works well, and where does the current approach break down?
7. Who owns changes to the process and its application integrations? Who operates it when something goes wrong?
8. Which data, identity, deployment, or audit requirements constrain a possible solution?
9. What happens if the process remains as it is for the next year?

Only after discussing the real example, briefly describe the product hypothesis and ask what would make a separate runtime harder to adopt than the current approach. Do not use enthusiasm for the concept as evidence of demand.

## Evidence to capture

For each conversation, record anonymized notes and separate:

- **Observation:** what the participant did, saw, measured, or tried in a specific example.
- **Interpretation:** what that example might mean for the product.
- **Unknown:** what still needs evidence.

Capture the process trigger, people and systems involved, handoff points, frequency, observed failure or delay, current workaround, cost of that workaround, process owner, integration owner, deployment and identity constraints, and the participant's exact language when permission allows. Avoid storing personal or confidential business data in the repository.

Use this per-interview record:

```text
Participant role (anonymized):
Recent process example:
Trigger and desired outcome:
People, applications, and manual channels:
Handoffs where ownership or status became unclear:
Frequency and observable cost:
Current workaround and what works well:
Process and integration owners:
Deployment, identity, and audit constraints:
Observations:
Interpretations:
Open questions:
```

## Decision rules for the next product revision

These are directional gates for deciding what to investigate or build next; passing them does not prove market size or product-market fit.

- **Problem:** keep the cross-boundary lifecycle problem prominent only if multiple independent participants can recount recent examples with a concrete cost or workaround. If the issue is rare or already handled adequately, narrow or replace the problem statement.
- **Audience:** keep application teams as the primary integration audience only if they own the relevant application boundary and have a reason existing tools do not fit. Otherwise, revisit the buyer, operator, and integration surface before broadening implementation.
- **First use case:** keep purchase approval only if real examples resemble the bounded handoff and do not require the MVP to become a procurement or finance product. Select a more representative request if evidence points elsewhere.
- **Runtime versus existing tools:** document the specific unmet need against the tools participants already use. If a separate runtime adds more deployment and integration work than it removes, do not expand the engine on the basis of an abstract platform idea.
- **Definition model:** do not add new step types or a general definition language until a second validated process requires behavior the seeded definition cannot express.
- **Self-hosting:** retain it as a primary deployment path only when data control, identity, network, or operating requirements support that choice.

After the round, write a short decision memo that updates the problem statement, target audience, first use case, MVP boundaries, and architecture decisions. Record evidence that contradicts the current direction as clearly as evidence that supports it.
