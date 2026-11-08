"""Black-box business scenarios against the real ASP.NET Core API + PostgreSQL.

Only run in an isolated Development environment. Uses Python standard library.
See .github/workflows/tests.yml for a reproducible CI environment.
"""
import json
from concurrent.futures import ThreadPoolExecutor
import os
import sys
import time
import unittest
import uuid
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

BASE = os.getenv("WORKFLOW_TEST_URL", "http://127.0.0.1:5195").rstrip("/")


def api(method, path, *, actor="requester@example.test", body=None, key=None):
    headers = {"Accept": "application/json"}
    if actor:
        headers["X-Demo-User"] = actor
    if key:
        headers["Idempotency-Key"] = key
    if body is not None:
        headers["Content-Type"] = "application/json"
    data = None if body is None else json.dumps(body).encode("utf-8")
    req = Request(BASE + path, data=data, headers=headers, method=method)
    try:
        with urlopen(req, timeout=10) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except HTTPError as error:
        raw = error.read()
        try:
            payload = json.loads(raw)
        except ValueError:
            payload = raw.decode("utf-8", errors="replace")
        return error.code, payload


def unique(prefix):
    return prefix + "-" + uuid.uuid4().hex[:12]


def purchase(amount=1500):
    return {
        "requesterReference": unique("PO"),
        "description": "Replacement computer",
        "amount": amount,
        "currency": "USD",
    }


def leave(days):
    return {
        "employeeId": unique("EMP"),
        "employeeName": "Test employee",
        "days": days,
        "reason": "Annual leave",
    }


def start(name, data, actor="requester@example.test", key=None):
    return api("POST", "/api/v1/workflows/" + name + "/instances",
               actor=actor, body=data, key=key or unique("start"))


def get_instance(instance_id, actor="requester@example.test"):
    return api("GET", "/api/v1/workflow-instances/" + instance_id, actor=actor)


def wait_for(instance_id, statuses, actor="requester@example.test", timeout=20):
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        status, doc = get_instance(instance_id, actor=actor)
        if status != 200:
            raise AssertionError("Instance read failed: " + repr((status, doc)))
        last = doc
        if doc["instance"]["status"] in statuses:
            return doc
        time.sleep(0.2)
    raise AssertionError("Timed out waiting for " + repr(statuses) +
                         "; last state: " + repr(last))


def work_item(instance_id, actor):
    status, items = api("GET", "/api/v1/work-items", actor=actor)
    if status != 200:
        raise AssertionError("Work list unavailable: " + repr((status, items)))
    matches = [item for item in items if item["workflowInstanceId"] == instance_id]
    if len(matches) != 1:
        raise AssertionError("Expected one pending task, got: " + repr(matches))
    return matches[0]


def complete(task_id, decision, actor, reviewer=None):
    payload = {"decision": decision}
    if reviewer is not None:
        payload["reviewer"] = reviewer
    return api("POST", "/api/v1/work-items/" + task_id + "/complete",
               actor=actor, body=payload)


class BusinessWorkflowTests(unittest.TestCase):
    def test_01_two_shipped_definitions_are_published(self):
        status, definitions = api("GET", "/api/v1/workflow-definitions")
        self.assertEqual(status, 200, definitions)
        names = {entry["name"] for entry in definitions}
        self.assertTrue({"purchase-approval", "employee-leave"} <= names)

    def test_02_purchase_below_threshold_auto_approves(self):
        status, created = start("purchase-approval", purchase(900))
        self.assertEqual(status, 202, created)
        result = wait_for(created["id"], {"completed"})
        self.assertEqual(result["instance"]["outcome"], "auto-approved")
        self.assertEqual(result["output"]["decision"], "auto-approved")
        self.assertIn("workflow-completed", [e["type"] for e in result["events"]])

    def test_03_manager_rejects_purchase_and_unauthorized_user_cannot_decide(self):
        status, created = start("purchase-approval", purchase(1800))
        self.assertEqual(status, 202, created)
        result = wait_for(created["id"], {"waiting-task"})
        self.assertEqual(result["instance"]["status"], "waiting-task")
        task = work_item(created["id"], "manager@example.test")
        self.assertEqual(task["assignedRole"], "manager")
        code, _ = complete(task["id"], "reject", "unrelated@example.test")
        self.assertEqual(code, 403)
        code, _ = complete(task["id"], "not-valid", "manager@example.test")
        self.assertEqual(code, 400)
        code, _ = complete(task["id"], "reject", "manager@example.test")
        self.assertEqual(code, 200)
        done = wait_for(created["id"], {"completed"})
        self.assertEqual(done["instance"]["outcome"], "rejected")
        self.assertEqual(done["output"]["decision"], "reject")
        code, _ = complete(task["id"], "approve", "manager@example.test")
        self.assertEqual(code, 409)

    def test_04_manager_can_approve_purchase(self):
        status, created = start("purchase-approval", purchase(1801))
        self.assertEqual(status, 202, created)
        wait_for(created["id"], {"waiting-task"})
        task = work_item(created["id"], "manager@example.test")
        self.assertEqual(complete(task["id"], "approve", "manager@example.test")[0], 200)
        done = wait_for(created["id"], {"completed"})
        self.assertEqual(done["instance"]["outcome"], "approved")
        self.assertEqual(done["output"]["decision"], "approve")

    def test_05_short_leave_only_needs_team_lead(self):
        status, created = start("employee-leave", leave(3))
        self.assertEqual(status, 202, created)
        wait_for(created["id"], {"waiting-task"})
        task = work_item(created["id"], "teamlead@example.test")
        self.assertEqual(task["assignedRole"], "team-lead")
        self.assertEqual(complete(task["id"], "approved", "teamlead@example.test", "Lead")[0], 200)
        done = wait_for(created["id"], {"completed"})
        self.assertEqual(done["instance"]["outcome"], "approved")
        self.assertEqual(done["output"]["reviewedBy"], "Lead")

    def test_06_extended_leave_routes_manager_then_hr(self):
        status, created = start("employee-leave", leave(9))
        self.assertEqual(status, 202, created)
        wait_for(created["id"], {"waiting-task"})
        first = work_item(created["id"], "department@example.test")
        self.assertEqual(first["assignedRole"], "department-manager")
        self.assertEqual(complete(first["id"], "approved", "department@example.test", "Dept")[0], 200)
        wait_for(created["id"], {"waiting-task"})
        second = work_item(created["id"], "hr@example.test")
        self.assertEqual(second["assignedRole"], "hr")
        self.assertNotEqual(first["id"], second["id"])
        self.assertEqual(complete(second["id"], "approved", "hr@example.test", "HR")[0], 200)
        done = wait_for(created["id"], {"completed"})
        self.assertEqual(done["instance"]["outcome"], "approved")
        self.assertEqual(done["output"]["reviewedBy"], "HR")

    def test_07_bad_request_is_rejected(self):
        bad = {"requesterReference": unique("PO"), "amount": 100}
        status, _ = start("purchase-approval", bad)
        self.assertEqual(status, 400)

    def test_08_start_is_idempotent_and_conflicting_payload_fails(self):
        key, body = unique("duplicate"), purchase(800)
        first_status, first = start("purchase-approval", body, key=key)
        same_status, same = start("purchase-approval", body, key=key)
        changed = dict(body, amount=2000)
        different_status, _ = start("purchase-approval", changed, key=key)
        self.assertEqual(first_status, 202)
        self.assertEqual(same_status, 200)
        self.assertEqual(first["id"], same["id"])
        self.assertEqual(different_status, 409)

    def test_09_anonymous_cannot_start_workflow(self):
        code, _ = api("POST", "/api/v1/workflows/purchase-approval/instances",
                      actor=None, body=purchase(), key=unique("key"))
        self.assertEqual(code, 401)

    def test_10_published_revision_is_immutable(self):
        code, definition = api("GET", "/api/v1/workflow-definitions/purchase-approval/1")
        self.assertEqual(code, 200, definition)
        code, _ = api("PUT", "/api/v1/workflow-definitions/purchase-approval/1",
                      body=definition, actor="manager@example.test")
        self.assertEqual(code, 409)

    def test_11_new_process_without_new_engine_code(self):
        name = unique("case")
        definition = {
            "schemaVersion": 1, "name": name, "revision": 1,
            "title": "Generic business decision", "description": "CI proof of extensibility",
            "inputSchema": {"type": "object", "properties": {"score": {"type": "integer"}},
                            "required": ["score"], "additionalProperties": False},
            "outputSchema": {"type": "object", "properties": {"decision": {"type": "string"}},
                             "required": ["decision"], "additionalProperties": False},
            "roles": [],
            "nodes": [
                {"id": "start", "type": "start", "config": {}, "transitions": [{"to": "route"}]},
                {"id": "route", "type": "gateway", "config": {}, "transitions": [
                    {"to": "accepted", "when": {"path": "$.input.score", "operator": "gte", "value": 60}},
                    {"to": "declined", "when": None}
                ]},
                {"id": "accepted", "type": "end", "config": {
                    "outcome": "accepted", "output": {"decision": "accepted"}}, "transitions": []},
                {"id": "declined", "type": "end", "config": {
                    "outcome": "declined", "output": {"decision": "declined"}}, "transitions": []}
            ]
        }
        path = "/api/v1/workflow-definitions/" + name + "/1"
        code, response = api("PUT", path, body=definition)
        self.assertIn(code, (200, 201), response)
        code, response = api("POST", path + "/publish", body={})
        self.assertIn(code, (200, 201), response)
        for score, expected in [(85, "accepted"), (10, "declined")]:
            code, instance = start(name, {"score": score})
            self.assertEqual(code, 202, instance)
            result = wait_for(instance["id"], {"completed"})
            self.assertEqual(result["instance"]["outcome"], expected)
            self.assertEqual(result["output"]["decision"], expected)



    def create_http_process(self, remote_path):
        connection = unique("connection")
        code, details = api("POST", "/api/v1/integrations", body={
            "key": connection, "name": "Local HTTPS fixture",
            "baseUrl": "https://localhost/", "authHeaderName": None,
            "authScheme": None, "secret": None,
        })
        self.assertEqual(code, 201, details)

        name = unique("httptest")
        definition = {
            "schemaVersion": 1, "name": name, "revision": 1,
            "title": "Outbound integration probe",
            "inputSchema": {
                "type": "object",
                "properties": {"reference": {"type": "string"}},
                "required": ["reference"], "additionalProperties": False
            },
            "outputSchema": {
                "type": "object",
                "properties": {"status": {"type": "string"},
                               "attempt": {"type": "integer"},
                               "idempotencyKey": {"type": "string"},
                               "reference": {"type": "string"}},
                "required": ["status", "attempt", "idempotencyKey", "reference"],
                "additionalProperties": False
            },
            "roles": [],
            "nodes": [
                {"id": "start", "type": "start", "config": {},
                 "transitions": [{"to": "send"}]},
                {"id": "send", "type": "http",
                 "config": {"integrationKey": connection, "method": "POST",
                            "path": remote_path, "request": {"reference": "$.input.reference"}},
                 "transitions": [{"to": "end"}]},
                {"id": "end", "type": "end", "config": {
                    "outcome": "delivered",
                    "output": {
                        "status": "$.actions.send.body.status",
                        "attempt": "$.actions.send.body.attempt",
                        "idempotencyKey": "$.actions.send.body.idempotencyKey",
                        "reference": "$.actions.send.body.reference"
                    }
                }, "transitions": []}
            ]
        }
        resource = "/api/v1/workflow-definitions/" + name + "/1"
        code, response = api("PUT", resource, body=definition)
        self.assertEqual(code, 200, response)
        code, response = api("POST", resource + "/publish", body={})
        self.assertEqual(code, 200, response)
        return name

    def test_12_http_integration_success_and_idempotency_header(self):
        flow = self.create_http_process("/ok/" + unique("request"))
        reference = unique("REF")
        code, created = start(flow, {"reference": reference})
        self.assertEqual(code, 202, created)
        done = wait_for(created["id"], {"completed", "failed"}, timeout=25)
        self.assertEqual(done["instance"]["status"], "completed", done)
        self.assertEqual(done["output"]["status"], "ok")
        self.assertEqual(done["output"]["attempt"], 1)
        self.assertEqual(done["output"]["reference"], reference)
        self.assertEqual(done["output"]["idempotencyKey"], created["id"] + ":send")

    def test_13_transient_http_failure_is_retried(self):
        flow = self.create_http_process("/retry/" + unique("request"))
        code, created = start(flow, {"reference": unique("REF")})
        self.assertEqual(code, 202, created)
        done = wait_for(created["id"], {"completed", "failed"}, timeout=25)
        self.assertEqual(done["instance"]["status"], "completed", done)
        self.assertEqual(done["output"]["attempt"], 2)
        self.assertIn("integration-retry-scheduled", [e["type"] for e in done["events"]])

    def test_14_permanent_http_failure_ends_without_infinite_retry(self):
        flow = self.create_http_process("/always-fail/" + unique("request"))
        code, created = start(flow, {"reference": unique("REF")})
        self.assertEqual(code, 202, created)
        done = wait_for(created["id"], {"completed", "failed"}, timeout=30)
        self.assertEqual(done["instance"]["status"], "failed", done)
        self.assertEqual(
            len([e for e in done["events"] if e["type"] == "integration-retry-scheduled"]), 2)
        self.assertIn("workflow-failed", [e["type"] for e in done["events"]])

    def test_15_connection_rejects_unapproved_destination(self):
        code, _ = api("POST", "/api/v1/integrations", body={
            "key": unique("blocked"), "name": "Not allowlisted",
            "baseUrl": "https://example.com/", "authHeaderName": None,
            "authScheme": None, "secret": None,
        })
        self.assertEqual(code, 400)



    def test_16_concurrent_identical_starts_create_one_instance(self):
        key, body = unique("racing-start"), purchase(2200)
        with ThreadPoolExecutor(max_workers=8) as pool:
            results = list(pool.map(
                lambda _: start("purchase-approval", body, key=key), range(8)))
        codes = [code for code, _ in results]
        self.assertEqual(codes.count(202), 1, results)
        self.assertTrue(all(code in (200, 202) for code in codes), results)
        self.assertEqual(len({response["id"] for _, response in results}), 1)

    def test_17_concurrent_task_completion_succeeds_only_once(self):
        code, instance = start("purchase-approval", purchase(2201))
        self.assertEqual(code, 202, instance)
        wait_for(instance["id"], {"waiting-task"})
        task = work_item(instance["id"], "manager@example.test")
        with ThreadPoolExecutor(max_workers=6) as pool:
            results = list(pool.map(
                lambda _: complete(task["id"], "approve", "manager@example.test"),
                range(6)))
        codes = [code for code, _ in results]
        self.assertEqual(codes.count(200), 1, results)
        self.assertTrue(all(code in (200, 409) for code in codes), results)
        done = wait_for(instance["id"], {"completed"})
        self.assertEqual(done["instance"]["outcome"], "approved")


def prepare_restart(path):
    status, created = start("purchase-approval", purchase(2300))
    if status != 202:
        raise AssertionError((status, created))
    wait_for(created["id"], {"waiting-task"})
    task = work_item(created["id"], "manager@example.test")
    Path(path).write_text(json.dumps({"id": created["id"], "task": task["id"]}), encoding="utf-8")
    print("Pending human task persisted before restart:", created["id"])


def verify_restart(path):
    record = json.loads(Path(path).read_text(encoding="utf-8"))
    pending = wait_for(record["id"], {"waiting-task"})
    if pending["instance"]["status"] != "waiting-task":
        raise AssertionError(pending)
    task = work_item(record["id"], "manager@example.test")
    if task["id"] != record["task"]:
        raise AssertionError("Pending task id changed on restart")
    code, response = complete(task["id"], "approve", "manager@example.test")
    if code != 200:
        raise AssertionError((code, response))
    done = wait_for(record["id"], {"completed"})
    if done["instance"]["outcome"] != "approved":
        raise AssertionError(done)
    print("Pending task survived restart and completed:", record["id"])


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "prepare-restart":
        prepare_restart(sys.argv[2])
    elif len(sys.argv) == 3 and sys.argv[1] == "verify-restart":
        verify_restart(sys.argv[2])
    else:
        unittest.main(verbosity=2)
