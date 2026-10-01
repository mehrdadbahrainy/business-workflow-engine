# Workflow Definition Format, Version 1

Definitions are JSON documents authored by the web designer and executed by the workflow runtime. The runtime accepts only node types and operators listed here; it does not execute user supplied scripts.

## Document shape

```json
{
  "schemaVersion": 1,
  "name": "example-process",
  "revision": 1,
  "title": "Example process",
  "description": "Optional explanation",
  "inputSchema": { "type": "object", "properties": {}, "required": [] },
  "outputSchema": { "type": "object", "properties": {}, "required": [] },
  "roles": [{ "id": "manager", "name": "Manager" }],
  "nodes": []
}
```

Names and node/role ids use lowercase letters, digits, and hyphens. Schema version 1 supports object contracts with `string`, `number`, `integer`, and `boolean` fields, `required` names, and `additionalProperties: false`. Published revisions are immutable.

## Nodes

Every node has a unique `id`, `type`, object `config`, and `transitions` array.

| Type | Purpose | Configuration |
| --- | --- | --- |
| `start` | Validate input and begin execution | One unconditional transition |
| `set` | Set instance variables | `values`: mapping object; one transition |
| `gateway` | Select one outgoing route | Conditional transitions and exactly one fallback transition |
| `userTask` | Wait for a person assigned by role | `role`, `title`, optional `input` mapping and `completionSchema`; one transition |
| `http` | Call an external HTTP service | `method`, configured `integrationKey`, relative `path`, optional `request` or `query` mapping; one transition |
| `end` | Complete and return business result | `outcome` and `output` mapping; no transitions |

Version 1 requires one start node, at least one reachable end node, valid targets, no unreachable nodes, and no cycles. Long-running instances persist their pinned definition revision and current position.

## Conditions and mappings

Conditions use a path, operator, and JSON literal value:

```json
{ "path": "$.input.amount", "operator": "gt", "value": 1000 }
```

Supported operators are `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, and `contains`. A missing path does not match numeric/string comparisons. Gateway transitions are checked in document order; the single transition without `when` is the fallback.

Mapping values are JSON literals, or a string beginning with `$.` to read the execution context:

- `$.input.someField` reads the original validated input.
- `$.variables.someName` reads a value set by a `set` node.
- `$.lastTask.decision` reads the last user task completion payload.
- `$.actions.node-id.body` reads the body returned by a successful HTTP action.

For example, a user task can map a title and display data from `$.input`, and an end node can map only the fields declared in `outputSchema`. Static strings beginning with `$.` are interpreted as references.

## Roles

The definition declares workflow role ids. A `userTask` references one id; the API exposes that task only to authenticated subjects whose configured role claim contains the role id. The identity provider remains responsible for user and group membership. Definitions do not grant themselves privileges.

## HTTP integrations

HTTP actions call a configured integration connection through a durable worker. An integration contains an immutable HTTPS base URL; a workflow supplies a relative path and mapped data. The destination host must be in `INTEGRATION_ALLOWED_HOSTS`. The Connections page can store an API credential, encrypted in PostgreSQL with the deployment `INTEGRATION_ENCRYPTION_KEY`; the UI and API never return secret values. Requests have a 15-second timeout, responses are limited to 1 MiB, and retryable failures receive at most three attempts with exponential delays. The engine sends an `Idempotency-Key` derived from the workflow instance and node id; external systems should honor it to avoid repeated side effects.

HTTP action execution is at least once across process interruption. A destination must support idempotency when repeating a request could cause a duplicate side effect.

## Example documents

- [Purchase approval](../../examples/purchase-approval/workflow.json)
- [Employee leave](../../examples/employee-leave/workflow.json)
