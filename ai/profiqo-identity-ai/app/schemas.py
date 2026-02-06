from __future__ import annotations

from typing import Any, Optional, List, Dict
from pydantic import BaseModel, Field


class AddressSnapshotDto(BaseModel):
    country: Optional[str] = None
    city: Optional[str] = None
    district: Optional[str] = None
    postalCode: Optional[str] = None
    addressLine1: Optional[str] = None
    addressLine2: Optional[str] = None
    fullName: Optional[str] = None


class CustomerChannelSummaryDto(BaseModel):
    channel: str
    ordersCount: int
    totalAmount: float
    currency: str


class CustomerDuplicateCandidateDto(BaseModel):
    customerId: str
    firstName: Optional[str] = None
    lastName: Optional[str] = None
    channels: List[CustomerChannelSummaryDto] = Field(default_factory=list)
    shippingAddress: Optional[AddressSnapshotDto] = None
    billingAddress: Optional[AddressSnapshotDto] = None


class ScoreRequest(BaseModel):
    a: CustomerDuplicateCandidateDto
    b: CustomerDuplicateCandidateDto


class ScoreResponse(BaseModel):
    score: float = Field(ge=0.0, le=1.0)
    rationale: str
    signals: Dict[str, Any] = Field(default_factory=dict)
