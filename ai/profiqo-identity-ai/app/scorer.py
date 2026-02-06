from __future__ import annotations

import os
from typing import Any, Dict

from openai import OpenAI
from pydantic import BaseModel, Field

from .schemas import ScoreRequest, ScoreResponse
from .text_norm import normalize_name, normalize_address


class LlmScore(BaseModel):
    score: float = Field(ge=0.0, le=1.0)
    rationale: str
    signals: Dict[str, Any] = Field(default_factory=dict)


def _candidate_features(req: ScoreRequest) -> Dict[str, Any]:
    a = req.a
    b = req.b

    a_name = normalize_name(a.firstName, a.lastName)
    b_name = normalize_name(b.firstName, b.lastName)

    a_ship = a.shippingAddress or a.billingAddress
    b_ship = b.shippingAddress or b.billingAddress

    a_addr = normalize_address(
        a_ship.country if a_ship else None,
        a_ship.city if a_ship else None,
        a_ship.district if a_ship else None,
        a_ship.postalCode if a_ship else None,
        a_ship.addressLine1 if a_ship else None,
        a_ship.addressLine2 if a_ship else None,
    )

    b_addr = normalize_address(
        b_ship.country if b_ship else None,
        b_ship.city if b_ship else None,
        b_ship.district if b_ship else None,
        b_ship.postalCode if b_ship else None,
        b_ship.addressLine1 if b_ship else None,
        b_ship.addressLine2 if b_ship else None,
    )

    return {
        "a_name": a_name,
        "b_name": b_name,
        "a_city": (a_ship.city if a_ship else None),
        "b_city": (b_ship.city if b_ship else None),
        "a_district": (a_ship.district if a_ship else None),
        "b_district": (b_ship.district if b_ship else None),
        "a_postal": (a_ship.postalCode if a_ship else None),
        "b_postal": (b_ship.postalCode if b_ship else None),
        "a_addr_norm": a_addr,
        "b_addr_norm": b_addr,
    }


class IdentityScoringService:
    def __init__(self) -> None:
        self._client = OpenAI()
        self._model = (os.getenv("OPENAI_MODEL") or "gpt-5.2").strip()

    def score(self, req: ScoreRequest) -> ScoreResponse:
        feats = _candidate_features(req)

        # Hard guard: name mismatch => low score, no need to call LLM
        if feats["a_name"] and feats["b_name"] and feats["a_name"] != feats["b_name"]:
            return ScoreResponse(
                score=0.05,
                rationale="Name mismatch after normalization.",
                signals={"a_name": feats["a_name"], "b_name": feats["b_name"]},
            )

        system = (
            "You are an identity resolution scorer for e-commerce customers. "
            "Return a score in [0,1] where 1 means same real-world person. "
            "Names are already required to be an exact match after normalization. "
            "Use address similarity signals (city, district, postal code, address line) and be robust to minor typos, missing postal codes, and abbreviations. "
            "If both addresses are empty/unknown, return a conservative score around 0.3 to 0.5, not 1."
        )

        user = {
            "instruction": "Score whether these two customer records represent the same person.",
            "features": feats,
            "raw": {
                "a": req.a.model_dump(),
                "b": req.b.model_dump(),
            },
        }

        # Structured output with Pydantic schema
        resp = self._client.responses.parse(
            model=self._model,
            input=[
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
            text_format=LlmScore,
        )

        out: LlmScore = resp.output_parsed
        score = max(0.0, min(1.0, float(out.score)))

        return ScoreResponse(score=score, rationale=out.rationale, signals=out.signals)
