#!/usr/bin/env python3
"""Resolve Google Form entry id for 'Codigo de participante' (text field)."""

from __future__ import annotations

import re
import sys
import urllib.parse
import urllib.request


def extract_form_id(url_or_id: str) -> tuple[str, bool]:
    raw = url_or_id.strip()
    if raw.startswith("http"):
        path = urllib.parse.urlparse(raw).path
        parts = [p for p in path.split("/") if p]
        for i, part in enumerate(parts):
            if part == "e" and i + 1 < len(parts):
                return parts[i + 1], True
            if part == "d" and i + 1 < len(parts):
                return parts[i + 1], False
        raise ValueError(f"Could not parse form id from URL: {url_or_id}")
    return raw, False


def build_view_url(form_id: str, published: bool) -> str:
    if published:
        return f"https://docs.google.com/forms/d/e/{form_id}/viewform"
    return f"https://docs.google.com/forms/d/{form_id}/viewform"


def resolve_entry_id(form_id: str, published: bool) -> str:
    view_url = build_view_url(form_id, published)
    html = urllib.request.urlopen(view_url, timeout=30).read().decode("utf-8", "replace")

    text_match = re.search(
        r"C[oó]digo de participante.{0,500}?\[\[(\d+),null",
        html,
        re.IGNORECASE | re.DOTALL,
    )
    if not text_match:
        raise RuntimeError(f"Could not find participant text field in form {form_id}")

    entry_id = text_match.group(1)
    test_url = f"{view_url}?usp=pp_url&entry.{entry_id}=P01"
    body = urllib.request.urlopen(test_url, timeout=30).read().decode("utf-8", "replace")
    if f"{entry_id},[&quot;P01&quot;]" not in body:
        raise RuntimeError(f"entry.{entry_id} did not prefill participant field for form {form_id}")

    return f"entry.{entry_id}"


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: fetch_form_entry_id.py <form_url_or_id>", file=sys.stderr)
        return 2

    try:
        form_id, published = extract_form_id(sys.argv[1])
        print(resolve_entry_id(form_id, published))
        return 0
    except Exception as exc:  # noqa: BLE001 - CLI tool
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
