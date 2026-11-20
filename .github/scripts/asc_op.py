#!/usr/bin/env python3
"""App Store Connect operations: guard, submit for review, pause/resume a phased release.

Apple permits exactly ONE version in review at a time. Cancelling a submission
developer-rejects it and sends you to the BACK of the review queue, so `guard` refuses
loudly instead of auto-cancelling; cancelling is a deliberate human act via store-ops.yml.

Submission uses the `reviewSubmissions` choreography (create -> add item -> submit), which
replaced the single-call `appStoreVersionSubmissions` endpoint.

Credentials (App Store Connect API key, non-expiring):
  APPSTORE_ISSUER_ID, APPSTORE_API_KEY_ID, APPSTORE_API_PRIVATE_KEY (the .p8 contents)
  APPSTORE_APP_ID  - the numeric app id from App Store Connect
Optional:
  APPSTORE_PLATFORM - IOS (default) | MAC_OS | TV_OS

Usage:
  asc_op.py guard
  asc_op.py submit --version X.Y.Z
  asc_op.py phased-pause
  asc_op.py phased-resume
  asc_op.py cancel-review
  asc_op.py status
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time

import jwt
import requests

BASE = "https://api.appstoreconnect.apple.com/v1"

# States that mean "a version is occupying the single review slot".
BLOCKING = {
    "WAITING_FOR_REVIEW",
    "IN_REVIEW",
    "PENDING_DEVELOPER_RELEASE",
    "PENDING_APPLE_RELEASE",
    "PROCESSING_FOR_DISTRIBUTION",
    "PROCESSING_FOR_APP_STORE",
    "READY_FOR_REVIEW",
}


def env(name: str) -> str:
    v = os.environ.get(name, "").strip()
    if not v:
        sys.exit(f"::error::{name} is not set.")
    return v


def platform() -> str:
    return os.environ.get("APPSTORE_PLATFORM", "").strip() or "IOS"


def token() -> str:
    key = env("APPSTORE_API_PRIVATE_KEY").replace("\\n", "\n")
    now = int(time.time())
    payload = {
        "iss": env("APPSTORE_ISSUER_ID"),
        "iat": now,
        "exp": now + 15 * 60,
        "aud": "appstoreconnect-v1",
    }
    return jwt.encode(
        payload, key, algorithm="ES256", headers={"kid": env("APPSTORE_API_KEY_ID")}
    )


def api(method: str, path: str, **kw):
    r = requests.request(
        method,
        f"{BASE}{path}",
        headers={"Authorization": f"Bearer {token()}", "Content-Type": "application/json"},
        timeout=60,
        **kw,
    )
    if r.status_code >= 400:
        sys.exit(f"::error::{method} {path} -> HTTP {r.status_code}: {r.text}")
    return r.json() if r.text else {}


def state_of(version: dict) -> str:
    """appStoreState was replaced by appVersionState; accept whichever is present."""
    at = version["attributes"]
    return at.get("appVersionState") or at.get("appStoreState") or "UNKNOWN"


def versions(app_id: str):
    """Versions for OUR platform only, newest first.

    An app record can cover IOS, MAC_OS and TV_OS; without the filter a macOS version in
    review would make the iOS guard refuse. Sorted so phased-release lookups take the
    newest live version rather than an arbitrary one.
    """
    data = api(
        "GET",
        f"/apps/{app_id}/appStoreVersions"
        f"?filter[platform]={platform()}&limit=50&sort=-versionString",
    ).get("data", [])
    return data


def blocking_versions(app_id: str):
    return [v for v in versions(app_id) if state_of(v) in BLOCKING]


def open_review_submissions(app_id: str):
    """Review submissions that are not yet completed or cancelled."""
    data = api(
        "GET",
        f"/apps/{app_id}/reviewSubmissions?filter[platform]={platform()}&limit=50",
    ).get("data", [])
    return [
        s for s in data
        if s["attributes"].get("state") not in ("COMPLETE", "CANCELING", "CANCELED")
    ]


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument(
        "action",
        choices=["guard", "submit", "phased-pause", "phased-resume", "cancel-review", "status"],
    )
    p.add_argument("--version")
    a = p.parse_args()
    app_id = env("APPSTORE_APP_ID")

    if a.action == "status":
        for v in versions(app_id):
            at = v["attributes"]
            print(f"{at.get('versionString')}  {state_of(v)}  (id {v['id']})")
        for s in open_review_submissions(app_id):
            print(f"reviewSubmission {s['id']}  state={s['attributes'].get('state')}")
        return

    if a.action == "guard":
        blocked = blocking_versions(app_id)
        open_subs = open_review_submissions(app_id)
        if blocked or open_subs:
            names = ", ".join(
                f"{v['attributes'].get('versionString')} ({state_of(v)})" for v in blocked
            ) or f"{len(open_subs)} open review submission(s)"
            sys.exit(
                "::error::App Store Connect already has a submission in flight: "
                f"{names}. Apple allows only one. Wait for it, or deliberately cancel with "
                "store-ops.yml (action: appstore-cancel-review) - cancelling developer-rejects "
                "the submission and returns you to the back of the review queue."
            )
        print("::notice::No blocking App Store version. Safe to submit.")
        return

    if a.action == "cancel-review":
        subs = open_review_submissions(app_id)
        if not subs:
            print("::notice::Nothing in review to cancel.")
            return
        for s in subs:
            api(
                "PATCH",
                f"/reviewSubmissions/{s['id']}",
                data=json.dumps(
                    {
                        "data": {
                            "type": "reviewSubmissions",
                            "id": s["id"],
                            "attributes": {"canceled": True},
                        }
                    }
                ),
            )
            print(f"::warning::Cancelled review submission {s['id']}.")
        return

    if a.action in ("phased-pause", "phased-resume"):
        state = "PAUSED" if a.action == "phased-pause" else "ACTIVE"
        live = [v for v in versions(app_id) if state_of(v) == "READY_FOR_SALE"]
        if not live:
            sys.exit("::error::No READY_FOR_SALE version with a phased release.")
        ph = api(
            "GET", f"/appStoreVersions/{live[0]['id']}/appStoreVersionPhasedRelease"
        ).get("data")
        if not ph:
            sys.exit("::error::That version has no phased release to control.")
        api(
            "PATCH",
            f"/appStoreVersionPhasedReleases/{ph['id']}",
            data=json.dumps(
                {
                    "data": {
                        "type": "appStoreVersionPhasedReleases",
                        "id": ph["id"],
                        "attributes": {"phasedReleaseState": state},
                    }
                }
            ),
        )
        print(f"::notice::Phased release set to {state}.")
        return

    if a.action == "submit":
        if not a.version:
            sys.exit("::error::--version is required for submit.")
        target = next(
            (v for v in versions(app_id)
             if v["attributes"].get("versionString") == a.version),
            None,
        )
        if target is None:
            sys.exit(
                f"::error::No App Store version {a.version} exists for platform {platform()}. "
                "Create it in App Store Connect (or let the TestFlight upload create it) "
                "before submitting."
            )

        # 1. A review submission for the app + platform.
        sub = api(
            "POST",
            "/reviewSubmissions",
            data=json.dumps(
                {
                    "data": {
                        "type": "reviewSubmissions",
                        "attributes": {"platform": platform()},
                        "relationships": {
                            "app": {"data": {"type": "apps", "id": app_id}}
                        },
                    }
                }
            ),
        )["data"]

        # 2. Put this version in it.
        api(
            "POST",
            "/reviewSubmissionItems",
            data=json.dumps(
                {
                    "data": {
                        "type": "reviewSubmissionItems",
                        "relationships": {
                            "reviewSubmission": {
                                "data": {"type": "reviewSubmissions", "id": sub["id"]}
                            },
                            "appStoreVersion": {
                                "data": {"type": "appStoreVersions", "id": target["id"]}
                            },
                        },
                    }
                }
            ),
        )

        # 3. Actually submit it. Until this PATCH the submission is only a draft.
        api(
            "PATCH",
            f"/reviewSubmissions/{sub['id']}",
            data=json.dumps(
                {
                    "data": {
                        "type": "reviewSubmissions",
                        "id": sub["id"],
                        "attributes": {"submitted": True},
                    }
                }
            ),
        )
        print(f"::notice::Submitted {a.version} for App Store review (submission {sub['id']}).")


if __name__ == "__main__":
    main()
