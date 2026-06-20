#!/usr/bin/env python3
"""Resolve Google Form entry id for 'Codigo de participante' (text field)."""

from __future__ import annotations

import re
import sys
import urllib.request

FORM_ID = "1XjG9GBr71tyhfF2sFWn7RcKfHkBAjCnufZJKZXoP6zA"
VIEW_URL = f"https://docs.google.com/forms/d/{FORM_ID}/viewform"


def main() -> int:
    html = urllib.request.urlopen(VIEW_URL, timeout=30).read().decode("utf-8", "replace")

    # Text question internal id appears as [[1810287026,null,...]] before choice entry.* ids.
    text_match = re.search(
        r"C[oó]digo de participante.{0,400}?\[\[(\d+),null",
        html,
        re.IGNORECASE | re.DOTALL,
    )
    if text_match:
        candidate = text_match.group(1)
        test_url = (
            f"https://docs.google.com/forms/d/{FORM_ID}/viewform"
            f"?usp=pp_url&entry.{candidate}=P01"
        )
        body = urllib.request.urlopen(test_url, timeout=30).read(80000).decode("utf-8", "replace")
        if "P01" in body:
            print(f"entry.{candidate}")
            return 0

    # Fallback: first entry.* before age question (642758300 is known age field in this form).
    for entry_id in re.findall(r"entry\.(\d+)", html):
        if entry_id == "642758300":
            break
        test_url = (
            f"https://docs.google.com/forms/d/{FORM_ID}/viewform"
            f"?usp=pp_url&entry.{entry_id}=P01"
        )
        body = urllib.request.urlopen(test_url, timeout=30).read(80000).decode("utf-8", "replace")
        if "P01" in body and "642758300" not in test_url:
            print(f"entry.{entry_id}")
            return 0

    print("ERROR: could not resolve entry id", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
