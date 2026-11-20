#!/usr/bin/env python3
"""Google Play track operations that do NOT re-upload the bundle.

`r0adkll/upload-google-play` always uploads: `releaseFiles` is mandatory at runtime and
the bundle is pushed before any track is assigned. So a second, gated "promote to
production" job using that action would re-upload an already-consumed versionCode and
Play would reject it with HTTP 400 "Version code N has already been used".

This script drives the Play Developer API v3 edit cycle directly
(edits.insert -> edits.tracks.update -> edits.commit), which moves an existing
versionCode between tracks and changes an in-progress rollout without touching the
binary.

Credentials come from the service-account JSON in $GOOGLE_PLAY_SERVICE_ACCOUNT_JSON.

Usage:
  play_track_op.py promote  --package P --version-code N --track production [--user-fraction 0.10]
  play_track_op.py rollout  --package P --track production --user-fraction 0.25
  play_track_op.py halt     --package P --track production
  play_track_op.py finalize --package P --track production
  play_track_op.py status   --package P --track production
"""

from __future__ import annotations

import argparse
import json
import os
import sys

import google.auth.transport.requests
from google.oauth2 import service_account

SCOPE = "https://www.googleapis.com/auth/androidpublisher"
BASE = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications"


def session():
    raw = os.environ.get("GOOGLE_PLAY_SERVICE_ACCOUNT_JSON", "").strip()
    if not raw:
        sys.exit("::error::GOOGLE_PLAY_SERVICE_ACCOUNT_JSON is empty.")
    creds = service_account.Credentials.from_service_account_info(
        json.loads(raw), scopes=[SCOPE]
    )
    return google.auth.transport.requests.AuthorizedSession(creds)


def check(resp, what):
    if resp.status_code >= 400:
        sys.exit(f"::error::{what} failed: HTTP {resp.status_code} {resp.text}")
    return resp.json() if resp.text else {}


def read_track(s, package, track, edit_id):
    r = s.get(f"{BASE}/{package}/edits/{edit_id}/tracks/{track}")
    return check(r, f"read track {track}")


def write_track(s, package, track, edit_id, releases):
    r = s.put(
        f"{BASE}/{package}/edits/{edit_id}/tracks/{track}",
        json={"track": track, "releases": releases},
    )
    return check(r, f"update track {track}")


def target_release(track_body):
    """The release a halt/rollout/finalize should act on.

    NOT simply the highest versionCode: a newer build saved as a `draft` (an aborted
    upload, or "Save as draft" in the Console) would win that comparison, and an
    emergency halt would then mark the draft halted while the bad build kept rolling.
    Prefer the one actually rolling out, then the live one, and only then fall back.
    """
    releases = track_body.get("releases") or []
    if not releases:
        sys.exit(f"::error::track '{track_body.get('track')}' has no releases.")

    def code_of(rel):
        return max((int(c) for c in rel.get("versionCodes", [])), default=0)

    for status in ("inProgress", "halted", "completed"):
        matching = [r for r in releases if r.get("status") == status]
        if matching:
            return max(matching, key=code_of)
    return max(releases, key=code_of)


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("action", choices=["promote", "rollout", "halt", "finalize", "status"])
    p.add_argument("--package", required=True)
    p.add_argument("--track", default="production")
    p.add_argument("--version-code")
    p.add_argument("--user-fraction", type=float)
    p.add_argument("--release-name")
    a = p.parse_args()

    s = session()

    if a.action == "status":
        edit = check(s.post(f"{BASE}/{a.package}/edits"), "create edit")
        body = read_track(s, a.package, a.track, edit["id"])
        print(json.dumps(body, indent=2))
        s.delete(f"{BASE}/{a.package}/edits/{edit['id']}")
        return

    edit = check(s.post(f"{BASE}/{a.package}/edits"), "create edit")
    edit_id = edit["id"]

    if a.action == "promote":
        if not a.version_code:
            sys.exit("::error::--version-code is required for promote.")
        release = {
            "versionCodes": [str(a.version_code)],
            "name": a.release_name or str(a.version_code),
        }
        if a.user_fraction is not None and a.user_fraction < 1.0:
            # inProgress + userFraction is the only combination that leaves a halt lever.
            # `completed` ships to 100% instantly and irreversibly.
            release["status"] = "inProgress"
            release["userFraction"] = a.user_fraction
        else:
            release["status"] = "completed"
        write_track(s, a.package, a.track, edit_id, [release])

    else:
        body = read_track(s, a.package, a.track, edit_id)
        releases = body.get("releases") or []
        rel = target_release(body)

        if a.action == "halt":
            rel["status"] = "halted"
            rel.pop("userFraction", None)
        elif a.action == "finalize":
            # Setting 100% is NOT the same as finalizing. An un-finalized rollout blocks
            # the next release.
            rel["status"] = "completed"
            rel.pop("userFraction", None)
        elif a.action == "rollout":
            if a.user_fraction is None:
                sys.exit("::error::--user-fraction is required for rollout.")
            rel["status"] = "inProgress"
            rel["userFraction"] = a.user_fraction

        # tracks.update REPLACES the whole Track resource, so send back every release -
        # the modified one plus the ones we did not touch. Writing just [rel] would delete
        # the track's other releases (e.g. the retained previous version).
        merged = [rel if r is rel else r for r in releases]
        write_track(s, a.package, a.track, edit_id, merged)

    committed = check(s.post(f"{BASE}/{a.package}/edits/{edit_id}:commit"), "commit edit")
    print(f"::notice::{a.action} on '{a.track}' committed (edit {committed.get('id', edit_id)}).")


if __name__ == "__main__":
    main()
