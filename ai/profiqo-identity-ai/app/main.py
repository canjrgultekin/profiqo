from __future__ import annotations

import os

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from .schemas import ScoreRequest, ScoreResponse
from .scorer import IdentityScoringService

app = FastAPI(title="Profiqo Identity AI", version="1.0.0")

# Dev friendly; tighten in prod
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=False,
    allow_methods=["*"],
    allow_headers=["*"],
)

svc = IdentityScoringService()


@app.get("/health")
def health():
    model = (os.getenv("OPENAI_MODEL") or "gpt-5.2").strip()
    return {"ok": True, "model": model}


@app.post("/identity/score", response_model=ScoreResponse)
def score(req: ScoreRequest):
    try:
        return svc.score(req)
    except Exception as ex:
        # Keep message short but helpful
        raise HTTPException(status_code=500, detail=f"Identity scoring failed: {str(ex)[:300]}")
