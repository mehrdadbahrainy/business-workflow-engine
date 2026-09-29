# Problem Statement

**Status:** Working product hypothesis; not yet validated with target users.

## The problem

Teams that handle recurring operational requests across people and existing business applications often rely on a mixture of forms, application-specific logic, email, chat, spreadsheets, and manual follow-up. When a request waits for a person or an external system, it can become unclear who owns the next action, what decisions have been made, and whether the request has actually reached an outcome.

The problem is not that organizations lack software or that every process should be automated. The problem is that some cross-boundary requests lack one dependable, visible lifecycle from submission to outcome.

## Who experiences it

The initial audience is a hypothesis:

- **Application teams** that own internal or line-of-business software and need to coordinate work across people and existing systems.
- **Operational users** who need to process, monitor, and resolve requests.
- **Approvers** who need a clear task, enough context to decide, and a recorded outcome.

The best-fit process is recurring, spans at least one human handoff and one system or operational action, and may remain in progress beyond a single web request. Organization size and industry are not yet established as part of the target market.

## How it is handled today

Organizations may use built-in workflows in business applications, low-code automation, dedicated process-orchestration products, custom application code, or manual coordination through email, chat, and spreadsheets. These are valid approaches and can work well in their intended context.

The project must not claim that existing products lack approvals, human tasks, execution history, or process visibility. For example, Power Automate documents approval flows across connected services, while Camunda documents human-task orchestration and process-instance visibility. The product opportunity, if validated, is a better fit for a specific audience and workflow model—not the invention of those capabilities.

## Product hypothesis

Build a self-hostable, open-source workflow runtime that application teams can integrate into existing business applications, paired with an operational interface where people can act on assigned work and inspect each request's progress and history.

The workflow accompanies a business request rather than replacing the systems that own the underlying business data. Its central responsibility is to make the request's lifecycle, current responsibility, decisions, and execution outcome explicit and traceable.

## Intended value

For each request, the people involved should be able to determine:

- its current state and the next responsible person or system;
- which decisions and actions have already occurred;
- whether it is waiting, completed, rejected, or needs attention;
- and how it reached its current outcome.

The value is not automation for its own sake. A workflow is useful only when this shared lifecycle reduces uncertainty or manual follow-up for a real process.

## Initial use-case candidate

Purchase approval is a candidate for the first vertical demonstration: an employee submits a request, a rule determines whether approval is required, a manager approves or rejects it, and the result is recorded and returned to the requester. This is a test scenario, not a validated market choice. Its real-world fit and the amount of procurement, finance, and ERP policy it would pull into scope still need to be checked.

## Project boundaries

The initial product is not intended to be:

- a replacement for CRM, ERP, procurement, or finance systems;
- a general-purpose integration catalog or an automation product that promises to connect everything;
- a task manager detached from an executable business process;
- a no-code workflow designer or an n8n clone;
- an AI-agent framework or a chatbot;
- a showcase for microservices or architectural patterns.

The first product hypothesis favors application-team integration and operational visibility. A full visual designer and broad connector ecosystem are outside the initial boundary unless user evidence changes that decision.

## Risks and open assumptions

1. **The category is established.** Existing products already offer overlapping capabilities. Open source and the selected technology stack alone are not a meaningful product distinction.
2. **The target user is still broad.** We need evidence about which application teams and operational processes feel this pain often enough to adopt a separate workflow layer.
3. **Purchase approval may be too generic or policy-heavy.** It is a convenient demonstration, but it may not be the strongest first real use case.
4. **A separate runtime may add more operational burden than it removes.** The product must make integration and day-to-day handling simpler for its intended audience.

## References for the current landscape

- [Power Automate: Create and test an approval workflow](https://learn.microsoft.com/en-us/power-automate/modern-approvals)
- [Camunda 8: Human-task orchestration](https://docs.camunda.io/docs/guides/orchestrate-human-tasks/)
- [Camunda 8: Processes and process orchestration](https://docs.camunda.io/docs/components/concepts/processes/)

These references establish feature overlap; they do not establish that the proposed product has a differentiated market position.
