"""Manual verification for identity sharding (B9 phase 2).

The development seeder creates one administrator, which lands on exactly one
shard. This script seeds a second account on the *other* shard using the same
schema, hashing and identifier rules the service uses, then performs a real
/api/v1/auth/login -> /connect/authorize -> /connect/token flow for both.

A successful run proves the login path follows the routing index into the shard
that owns the account, instead of assuming every account is in one database.
"""

import base64
import hashlib
import hmac
import json
import os
import shlex
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

IDP = os.environ.get("SHARD_IDP", "http://127.0.0.1:5511")
PSQL = os.environ.get("SHARD_PSQL_CMD", "")
DB_PASSWORD = os.environ.get("SHARD_DB_PASSWORD", "nerves")
GLOBAL_DSN = os.environ.get("SHARD_GLOBAL_DSN", "")
SHARD0_DSN = os.environ.get("SHARD_ZERO_DSN", "")
SHARD1_DSN = os.environ.get("SHARD_ONE_DSN", "")
REDIRECT = os.environ.get("SHARD_REDIRECT", "http://127.0.0.1:5300/oidc-callback.html")
CLIENT_ID = os.environ.get("SHARD_CLIENT_ID", "nerves-admin")
SECOND_USERNAME = "shard2"
SECOND_PASSWORD = "Str0ng!Pass123"
DEV_IDENTIFIER_KEY = "ZGV2LW9ubHktaWRlbnRpZmllci1rZXktMzJieXRlcy0="


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


opener = urllib.request.build_opener(NoRedirect)


def call(url, data=None, headers=None, method=None):
    request = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    try:
        response = opener.open(request)
        return response.status, dict(response.headers), response.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers), error.read().decode()


def fail(message):
    print("FAIL:", message)
    sys.exit(1)


def expect(actual, expected, label):
    if actual != expected:
        fail(label + ": expected " + str(expected) + ", got " + str(actual))
    print("   ok  " + label + " -> " + str(actual))


def col(name):
    return chr(34) + name + chr(34)


def lit(value):
    return "'" + str(value).replace("'", "''") + "'"


def psql(dsn, sql):
    if not PSQL:
        fail("SHARD_PSQL_CMD is empty")

    # dsn is "Host=...;Port=...;Database=...;Username=...;Password=..." (the
    # .NET connection string). Convert it to libpq arguments.
    parts = dict(
        part.split("=", 1) for part in dsn.split(";") if "=" in part)
    command = shlex.split(PSQL)
    if command[:1] == ["docker"]:
        # Inside the container the server listens on the socket/5432; the
        # host-mapped port in the .NET connection string does not apply.
        args = ["-U", parts.get("Username", "nerves"),
                "-d", parts.get("Database", "postgres"), "-tAc", sql]
    else:
        args = ["-h", parts.get("Host", "127.0.0.1"),
                "-p", parts.get("Port", "5432"),
                "-U", parts.get("Username", "nerves"),
                "-d", parts.get("Database", "postgres"), "-tAc", sql]
    result = subprocess.run(
        command + args,
        capture_output=True, text=True, check=False,
        env={**os.environ, "PGPASSWORD": parts.get("Password", DB_PASSWORD)})
    if result.returncode != 0:
        fail("psql failed: " + result.stderr.strip())
    return result.stdout.strip()


def identifier_hash(identifier_type, realm, value):
    key = base64.b64decode(DEV_IDENTIFIER_KEY)
    normalized = identifier_type + "|" + realm.lower() + "|" + value.strip().lower()
    return hmac.new(key, normalized.encode(), hashlib.sha256).hexdigest()


def password_hash(password):
    salt = os.urandom(16)
    key = hashlib.pbkdf2_hmac("sha512", password.encode(), salt, 100_000, dklen=32)
    return ("pbkdf2-sha512$100000$" + base64.b64encode(salt).decode()
            + "$" + base64.b64encode(key).decode())


def shard_of(account_id):
    digest = hashlib.sha256(account_id.replace("-", "").encode()).digest()
    return int.from_bytes(digest[:4], "little") % 2


def find_guid_for_shard(shard):
    while True:
        candidate = str(uuid.uuid4())
        if shard_of(candidate) == shard:
            return candidate


def account_username(dsn, account_id):
    sql = ("select " + col("Username") + " from identity.network_account where "
           + col("Id") + " = " + lit(account_id))
    return psql(dsn, sql)


def seed_second_account(opposite_shard, entity_id):
    account_id = find_guid_for_shard(opposite_shard)
    dsn = SHARD0_DSN if opposite_shard == 0 else SHARD1_DSN

    psql(dsn,
         "insert into identity.network_account ("
         + col("Id") + ", " + col("LegalEntityId") + ", " + col("Realm") + ", "
         + col("Username") + ", " + col("Email") + ", " + col("PhoneNumber") + ", "
         + col("DisplayName") + ", " + col("Type") + ", " + col("Status") + ", "
         + col("IsPrimary") + ", " + col("CreatedAt") + ", " + col("UpdatedAt") + ", "
         + col("DisabledAt") + ") values ("
         + lit(account_id) + ", " + lit(entity_id) + ", " + lit("nerves") + ", "
         + lit(SECOND_USERNAME) + ", " + lit(SECOND_USERNAME + "@acme.test") + ", null, "
         + lit(SECOND_USERNAME) + ", 1, 1, false, now(), now(), null)")

    psql(dsn,
         "insert into identity.account_credential ("
         + col("Id") + ", " + col("AccountId") + ", " + col("Type") + ", "
         + col("Secret") + ", " + col("Algorithm") + ", " + col("CreatedAt") + ", "
         + col("UpdatedAt") + ", " + col("LastUsedAt") + ", " + col("ActivatedAt")
         + ") values ("
         + lit(str(uuid.uuid4())) + ", " + lit(account_id) + ", 1, "
         + lit(password_hash(SECOND_PASSWORD)) + ", " + lit("pbkdf2-sha512")
         + ", now(), now(), null, null)")

    for identifier_type, value in (("username", SECOND_USERNAME),
                                   ("email", SECOND_USERNAME + "@acme.test")):
        psql(GLOBAL_DSN,
             "insert into identity.routing_index ("
             + col("IdentifierHash") + ", " + col("IdentifierType") + ", " + col("Realm") + ", "
             + col("AccountId") + ", " + col("LegalEntityId") + ", " + col("Shard") + ", "
             + col("CreatedAt") + ") values ("
             + lit(identifier_hash(identifier_type, "nerves", value)) + ", "
             + lit(identifier_type) + ", " + lit("nerves") + ", " + lit(account_id) + ", "
             + lit(entity_id) + ", " + str(opposite_shard) + ", now())")

    return account_id


def login(username, password):
    status, _, body = call(
        IDP + "/api/v1/auth/login",
        json.dumps({"username": username, "password": password}).encode(),
        {"Content-Type": "application/json"})
    return status, (json.loads(body) if body else {})


def token_subject(username, password):
    status, payload = login(username, password)
    expect(status, 200, "login as " + username)
    if payload.get("mfaRequired"):
        fail("the seeded account unexpectedly requires MFA")

    verifier = base64.urlsafe_b64encode(os.urandom(32)).decode().rstrip("=")
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    query = urllib.parse.urlencode({
        "client_id": CLIENT_ID,
        "response_type": "code",
        "scope": "openid profile email nerves.api",
        "redirect_uri": REDIRECT,
        "state": "shard-smoke",
        "code_challenge": challenge,
        "code_challenge_method": "S256",
        "login_ticket": payload["loginTicket"],
    })
    status, headers, _ = call(IDP + "/connect/authorize?" + query)
    expect(status, 302, "authorize for " + username)

    code = urllib.parse.parse_qs(urllib.parse.urlparse(headers.get("Location", "")).query).get("code", [None])[0]
    if not code:
        fail("authorize did not return a code")

    form = urllib.parse.urlencode({
        "grant_type": "authorization_code",
        "code": code,
        "redirect_uri": REDIRECT,
        "client_id": CLIENT_ID,
        "code_verifier": verifier,
    }).encode()
    status, _, body = call(IDP + "/connect/token", form,
                           {"Content-Type": "application/x-www-form-urlencoded"})
    expect(status, 200, "token for " + username)
    tokens = json.loads(body)

    status, _, body = call(IDP + "/connect/userinfo",
                           headers={"Authorization": "Bearer " + tokens["access_token"]})
    expect(status, 200, "userinfo for " + username)
    return json.loads(body)["sub"]


def main():
    admin_id = psql(
        GLOBAL_DSN,
        "select " + col("AccountId") + " from identity.routing_index where "
        + col("IdentifierType") + " = " + lit("username") + " and "
        + col("Realm") + " = " + lit("nerves") + " and "
        + col("IdentifierHash") + " = " + lit(identifier_hash("username", "nerves", "admin")))
    if not admin_id:
        fail("the routed administrator account is missing from the global database")

    admin_shard = shard_of(admin_id)
    print("   admin account " + admin_id + " lives on shard " + str(admin_shard))

    entity_id = psql(GLOBAL_DSN,
                     "select " + col("LegalEntityId") + " from identity.routing_index where "
                     + col("IdentifierHash") + " = "
                     + lit(identifier_hash("username", "nerves", "admin")))
    if not entity_id:
        fail("the administrator has no legal entity in the routing index")

    admin_dsn = SHARD0_DSN if admin_shard == 0 else SHARD1_DSN
    expect(account_username(admin_dsn, admin_id), "admin", "admin row found in its shard")

    second_id = seed_second_account(1 - admin_shard, entity_id)
    print("   seeded " + SECOND_USERNAME + " " + second_id + " on shard " + str(1 - admin_shard))

    subject = token_subject("admin", "ChangeMe!123")
    expect(subject, admin_id, "admin token subject")

    subject = token_subject(SECOND_USERNAME, SECOND_PASSWORD)
    expect(subject, second_id, "second account token subject")

    second_dsn = SHARD0_DSN if (1 - admin_shard) == 0 else SHARD1_DSN
    expect(account_username(second_dsn, second_id), SECOND_USERNAME,
           "second account stored on its shard")

    print("dual-database sharding smoke passed")


if __name__ == "__main__":
    main()