# nerves-sdk (Python)

Chainable Python client for the Nerves management API (`/api/v1`). Python 3.10+;
depends only on `requests`.

```bash
pip install nerves-sdk
```

## Usage

```python
import nerves

with nerves.client("https://accounts.example.com", token="pat-...") as c:
    account = (c.accounts.create()
        .for_entity(entity_id)
        .with_username("alice")
        .with_email("alice@example.com")
        .with_roles("member")
        .send())

    page = (c.accounts.list()
        .for_entity(entity_id)
        .with_status(nerves.AccountStatus.ACTIVE)
        .filter("username", "alice")
        .sort("-createdAt")
        .limit(50)
        .send())

    for item in page.items:
        print(item.username)

    # List builders are iterators that follow the opaque cursor automatically.
    for item in c.accounts.list().for_entity(entity_id):
        print(item.username)
```

Every builder is a frozen dataclass: each `with_*` / `for_*` / `filter` call returns a
new builder, so builders can be stored and forked safely. The package exposes
`nerves.client(base_url, token=None, *, session=None, ...)`, and `NervesClient` is a
context manager.

## What is wired to what

| SDK call | HTTP |
| --- | --- |
| `accounts.create()...send()` | `POST /api/v1/accounts` |
| `accounts.get(id).send()` | `GET /api/v1/accounts/{id}` |
| `accounts.list()...send()` | `GET /api/v1/accounts?limit=&cursor=&filter[key]=&sort=` |
| `accounts.update(id)...send()` | `PATCH /api/v1/accounts/{id}` |
| `accounts.delete(id).send()` | `DELETE /api/v1/accounts/{id}` |
| `legal_entities.*` | `/api/v1/legal-entities` |
| `roles.*` | `/api/v1/roles` |

Creates send a fresh `Idempotency-Key` unless one is supplied with
`with_idempotency_key(...)`. Updates and deletes accept `if_match(etag)` for
`If-Match`. Non-success responses raise `nerves.NervesApiException` carrying
`status`, `type`, `trace_id`, and the parsed `ProblemDetails`.

## Development

```bash
python -m venv .venv
.venv/bin/pip install -e '.[dev]'
.venv/bin/pytest
```
