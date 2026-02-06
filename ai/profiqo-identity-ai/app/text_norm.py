from __future__ import annotations

import re


_TR_MAP = str.maketrans(
    {
        "ı": "i",
        "ş": "s",
        "ğ": "g",
        "ü": "u",
        "ö": "o",
        "ç": "c",
        "İ": "i",
        "Ş": "s",
        "Ğ": "g",
        "Ü": "u",
        "Ö": "o",
        "Ç": "c",
    }
)

_WS_RE = re.compile(r"\s+")
_KEEP_RE = re.compile(r"[^a-z0-9\s]+")


def normalize_token(s: str | None) -> str:
    s = (s or "").strip().lower().translate(_TR_MAP)
    s = _KEEP_RE.sub(" ", s)
    s = _WS_RE.sub(" ", s).strip()
    return s


def normalize_name(first: str | None, last: str | None) -> str:
    return normalize_token(f"{first or ''} {last or ''}".strip())


def normalize_address(country: str | None, city: str | None, district: str | None, postal: str | None, a1: str | None, a2: str | None) -> str:
    return normalize_token(f"{country or ''} {city or ''} {district or ''} {postal or ''} {a1 or ''} {a2 or ''}")
