"""Sessions, authorization, audit, compliance, notifications and events."""

from __future__ import annotations

import pytest

from nerves import NervesApiException

from helpers import (
    ACCOUNT_ID,
    AUDIT_EVENT,
    CONSENT,
    CONSENT_ID,
    GROUP,
    GROUP_ID,
    NOTIFICATION,
    NOTIFICATION_ID,
    SESSION,
    SESSION_ID,
    make_client,
)


def test_sessions_are_listed_and_revoked() -> None:
    client, session = make_client()
    session.enqueue(200, [SESSION]).enqueue(200)

    sessions = client.sessions.list(ACCOUNT_ID).send()
    client.sessions.revoke(SESSION_ID).send()

    assert sessions[0].device_id == "web-1"
    assert session.calls[0].url == f"https://nerves.test/api/v1/accounts/{ACCOUNT_ID}/sessions"
    assert session.calls[1].method == "DELETE"
    assert session.calls[1].url == f"https://nerves.test/api/v1/sessions/{SESSION_ID}"


def test_permissions_are_listed_and_created() -> None:
    permission = {"id": "0199a0c0-0000-7000-8000-000000000040", "code": "accounts.read", "description": "Read accounts"}
    client, session = make_client()
    session.enqueue(200, [permission]).enqueue(201, permission)

    listed = client.permissions.list().send()
    created = client.permissions.create().with_code("accounts.read").with_description("Read accounts").send()

    assert listed[0].code == "accounts.read"
    assert created.code == "accounts.read"
    call = session.calls[1]
    assert call.headers["Idempotency-Key"]
    assert call.json == {"code": "accounts.read", "description": "Read accounts"}


def test_permission_create_requires_a_code() -> None:
    client, _ = make_client()

    with pytest.raises(ValueError, match="with_code"):
        client.permissions.create().send()


def test_groups_are_listed_created_and_members_managed() -> None:
    client, session = make_client()
    session.enqueue(200, [GROUP]).enqueue(201, GROUP).enqueue(200, {**GROUP, "memberCount": 3}).enqueue(200).enqueue(200)

    groups = client.groups.list().in_realm("default").send()
    client.groups.create().in_realm("default").with_code("ops").with_name("Operations").send()
    detail = client.groups.get(GROUP_ID).send()
    client.groups.add_member(GROUP_ID).with_account(ACCOUNT_ID).send()
    client.groups.remove_member(GROUP_ID, ACCOUNT_ID).send()

    assert groups[0].code == "ops"
    assert detail.member_count == 3
    assert dict(session.calls[0].params or [])["realm"] == "default"
    assert session.calls[1].json == {"realm": "default", "code": "ops", "name": "Operations"}
    assert session.calls[3].json == {"accountId": ACCOUNT_ID}
    assert session.calls[4].method == "DELETE"
    assert session.calls[4].url == f"https://nerves.test/api/v1/groups/{GROUP_ID}/members/{ACCOUNT_ID}"


def test_decisions_check_and_stats() -> None:
    client, session = make_client()
    session.enqueue(200, {"allowed": True, "source": "local", "authorizationVersion": 7, "permission": "accounts.read"})
    session.enqueue(200, {"entries": 2, "hits": 10, "misses": 2, "hitRate": 0.8333})

    decision = client.decisions.check("accounts.read").for_account(ACCOUNT_ID).send()
    stats = client.decisions.stats().send()

    assert decision.allowed is True
    assert decision.authorization_version == 7
    assert stats.hit_rate == pytest.approx(0.8333)
    assert session.calls[0].json == {
        "accountId": ACCOUNT_ID,
        "legalEntityId": None,
        "permission": "accounts.read",
    }


def test_audit_list_verify_and_export() -> None:
    client, session = make_client()
    session.enqueue(200, {"items": [AUDIT_EVENT], "nextCursor": "c2"})
    session.enqueue(200, {"items": [AUDIT_EVENT], "nextCursor": None})
    session.enqueue(200, {"valid": False, "brokenAt": AUDIT_EVENT["id"], "reason": "hash_mismatch"})
    session.enqueue(
        200,
        "id,stream\n1,identity\n",
        headers={
            "Content-Type": "text/csv; charset=utf-8",
            "Content-Disposition": 'attachment; filename="nerves-audit-20260101-000000.csv"',
        },
    )

    events = list(client.audit.list().in_stream("identity").with_action("account.created"))
    result = client.audit.verify().in_stream("identity").send()
    file = client.audit.export().as_("csv").limit(10).send()

    assert len(events) == 2
    assert dict(session.calls[0].params or [])["stream"] == "identity"
    assert dict(session.calls[1].params or [])["cursor"] == "c2"
    assert result.reason == "hash_mismatch"
    assert file.content_type == "text/csv"
    assert file.file_name == "nerves-audit-20260101-000000.csv"
    assert b"id,stream" in file.content
    assert dict(session.calls[3].params or [])["format"] == "csv"


def test_consents_are_listed_granted_and_revoked() -> None:
    client, session = make_client()
    session.enqueue(200, [CONSENT]).enqueue(201, CONSENT).enqueue(200, CONSENT)

    listed = client.compliance.consents().for_subject("alice").send()
    client.compliance.grant_consent().for_subject("alice").with_purpose("marketing").with_version("v1").send()
    client.compliance.revoke_consent(CONSENT_ID).with_reason("withdrawn").send()

    assert listed[0].purpose == "marketing"
    assert dict(session.calls[0].params or [])["subjectId"] == "alice"
    assert session.calls[1].json == {
        "subjectId": "alice",
        "purpose": "marketing",
        "version": "v1",
        "expiresAt": None,
    }
    assert session.calls[2].json == {"reason": "withdrawn"}
    assert session.calls[2].url == f"https://nerves.test/api/v1/compliance/consents/{CONSENT_ID}/revoke"


def test_grant_consent_requires_three_fields() -> None:
    client, _ = make_client()

    with pytest.raises(ValueError, match="for_subject"):
        client.compliance.grant_consent().with_purpose("p").with_version("v").send()
    with pytest.raises(ValueError, match="with_purpose"):
        client.compliance.grant_consent().for_subject("s").with_version("v").send()
    with pytest.raises(ValueError, match="with_version"):
        client.compliance.grant_consent().for_subject("s").with_purpose("p").send()


def test_retention_policies_and_summary() -> None:
    policy = {
        "id": "0199a0c0-0000-7000-8000-000000000071",
        "category": "audit",
        "retentionDays": 180,
        "legalBasis": "CSL",
        "updatedAt": "2026-01-01T00:00:00Z",
    }
    client, session = make_client()
    session.enqueue(200, [policy]).enqueue(200, {**policy, "retentionDays": 365})
    session.enqueue(200, {"consents": 10, "active": 8, "revoked": 2, "retentionPolicies": 3})

    policies = client.compliance.retention_policies().send()
    updated = client.compliance.upsert_retention_policy("audit").with_retention(365).with_legal_basis("CSL").send()
    summary = client.compliance.summary().send()

    assert policies[0].retention_days == 180
    assert updated.retention_days == 365
    assert summary.active == 8
    assert session.calls[1].method == "PUT"
    assert session.calls[1].json == {"retentionDays": 365, "legalBasis": "CSL"}


def test_subjects_export_erase_and_erasures() -> None:
    client, session = make_client()
    session.enqueue(200, {"subjectId": "alice", "exportedAt": "2026-01-01T00:00:00Z", "account": None, "consents": [], "retentionPolicies": []})
    session.enqueue(200, {
        "id": "0199a0c0-0000-7000-8000-000000000080",
        "subjectId": "alice",
        "status": "completed",
        "consentRevoked": 2,
        "accountDisabled": True,
    })
    session.enqueue(200, [{
        "id": "0199a0c0-0000-7000-8000-000000000080",
        "status": "completed",
        "reason": "gdpr",
        "requestedAt": "2026-01-01T00:00:00Z",
        "completedAt": "2026-01-01T00:01:00Z",
    }])

    exported = client.subjects.export("alice").send()
    result = client.subjects.erase("alice").with_reason("gdpr").send()
    erasures = client.subjects.erasures("alice").send()

    assert exported["subjectId"] == "alice"
    assert result.account_disabled is True
    assert session.calls[1].json == {"reason": "gdpr"}
    assert erasures[0].reason == "gdpr"


def test_notifications_send_list_and_get() -> None:
    client, session = make_client()
    session.enqueue(201, NOTIFICATION).enqueue(200, {"items": [NOTIFICATION], "nextCursor": None})
    session.enqueue(200, NOTIFICATION)

    sent = client.notifications.send_email().to("alice@example.com").with_template("welcome").with_value("name", "Alice").send()
    page = client.notifications.list().with_status("pending").to("alice@example.com").limit(5).send()
    one = client.notifications.get(NOTIFICATION_ID).send()

    assert sent.template == "welcome"
    assert page.items[0].status == "pending"
    assert one.to == "alice@example.com"
    call = session.calls[0]
    assert call.headers["Idempotency-Key"]
    assert call.json == {"to": "alice@example.com", "template": "welcome", "values": {"name": "Alice"}}
    assert dict(session.calls[1].params or [])["status"] == "pending"


def test_events_read_the_incremental_stream() -> None:
    client, session = make_client()
    session.enqueue(200, [{
        "id": "0199a0c0-0000-7000-8000-0000000000a0",
        "type": "account.created",
        "accountId": ACCOUNT_ID,
        "legalEntityId": None,
        "payload": "{}",
        "occurredAt": "2026-01-01T00:00:00Z",
        "dispatchedAt": None,
    }])

    events = client.events.list().for_account(ACCOUNT_ID).since("2026-01-01T00:00:00Z").limit(50).send()

    assert events[0].type == "account.created"
    params = dict(session.calls[0].params or [])
    assert params["accountId"] == ACCOUNT_ID
    assert params["limit"] == "50"
    assert params["after"].startswith("2026-01-01T00:00:00")


def test_new_resources_share_the_error_contract() -> None:
    client, session = make_client()
    session.enqueue(409, {"title": "Conflict", "status": 409, "detail": "duplicate"}, reason="Conflict")

    with pytest.raises(NervesApiException) as raised:
        client.groups.create().in_realm("default").with_code("ops").with_name("Operations").send()

    assert raised.value.status == 409
    assert raised.value.detail == "duplicate"

def test_roles_are_assigned_and_revoked() -> None:
    client, session = make_client()
    session.enqueue(200).enqueue(200)
    role_id = "0199a0c0-0000-7000-8000-000000000020"

    client.roles.assign(role_id).with_subject("account", ACCOUNT_ID).send()
    client.roles.revoke(role_id).with_subject("group", ACCOUNT_ID).send()

    assert session.calls[0].url == f"https://nerves.test/api/v1/roles/{role_id}/assign"
    assert session.calls[0].json == {"subjectType": "account", "subjectId": ACCOUNT_ID}
    assert session.calls[1].url == f"https://nerves.test/api/v1/roles/{role_id}/revoke"


def test_role_assignment_requires_a_subject() -> None:
    client, _ = make_client()

    with pytest.raises(ValueError, match="with_subject"):
        client.roles.assign("0199a0c0-0000-7000-8000-000000000020").send()
