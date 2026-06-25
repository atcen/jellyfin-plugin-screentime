#!/usr/bin/env python3
"""Add (or replace) a version entry in the Jellyfin plugin manifest.

Usage:
    update_manifest.py --version 1.0.1.0 --checksum <md5> --source-url <url> \
        --target-abi 10.11.0.0 --timestamp 2026-06-25T12:00:00Z \
        --changelog "..." [--manifest manifest.json]
"""
import argparse
import json
import sys


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True)
    p.add_argument("--checksum", required=True)
    p.add_argument("--source-url", required=True)
    p.add_argument("--target-abi", required=True)
    p.add_argument("--timestamp", required=True)
    p.add_argument("--changelog", default="")
    p.add_argument("--manifest", default="manifest.json")
    args = p.parse_args()

    with open(args.manifest, encoding="utf-8") as fh:
        manifest = json.load(fh)

    if not manifest:
        print("manifest is empty", file=sys.stderr)
        return 1

    entry = manifest[0]
    versions = entry.setdefault("versions", [])

    # Remove any existing entry for this version, then prepend the new one (newest first).
    versions = [v for v in versions if v.get("version") != args.version]
    versions.insert(0, {
        "version": args.version,
        "changelog": args.changelog,
        "targetAbi": args.target_abi,
        "sourceUrl": args.source_url,
        "checksum": args.checksum,
        "timestamp": args.timestamp,
    })
    entry["versions"] = versions

    with open(args.manifest, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=4, ensure_ascii=False)
        fh.write("\n")

    print(f"manifest updated: {args.version} ({args.checksum})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
