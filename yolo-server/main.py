"""
EcoCharge YOLO Bottle Detection Server
--------------------------------------
Loads v2.pt (YOLOv8 trained on 7 bottle-size classes) and exposes:

  POST /detect   — accepts base64 image, returns detections + points
  GET  /health   — liveness probe

Points conversion (Table 2.3):
  190ml  → 1 pt   (0.5 rounded up)
  237ml  → 1 pt
  290ml  → 1 pt
  500ml  → 3 pts
  1000ml → 5 pts
  1500ml → 8 pts
  1750ml → 8 pts
"""

import base64
import io
import os
from pathlib import Path

import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from PIL import Image

from ultralytics import YOLO

# ── Load model ─────────────────────────────────────────────────────────────────
MODEL_PATH = Path(__file__).parent / "v2.pt"
if not MODEL_PATH.exists():
    raise FileNotFoundError(f"Model not found at {MODEL_PATH}.")

print(f"[EcoCharge YOLO] Loading model from {MODEL_PATH} …")
model = YOLO(str(MODEL_PATH))
print(f"[EcoCharge YOLO] Model loaded. Classes: {model.names}")

# ── Points & category tables ───────────────────────────────────────────────────
POINTS_MAP: dict[str, float] = {
    "190ml":  1.0,
    "237ml":  1.0,
    "290ml":  1.0,
    "500ml":  3.0,
    "1000ml": 5.0,
    "1500ml": 8.0,
    "1750ml": 8.0,
}

CATEGORY_MAP: dict[str, str] = {
    "190ml":  "small",
    "237ml":  "small",
    "290ml":  "small",
    "500ml":  "medium",
    "1000ml": "medium",
    "1500ml": "large",
    "1750ml": "large",
}

# v2.pt produces very low confidence (~0.05 max) — go all the way to 0.001
CONF_LADDER = [0.05, 0.01, 0.005, 0.001, 0.0005, 0.0001]

# ── FastAPI ────────────────────────────────────────────────────────────────────
app = FastAPI(title="EcoCharge Bottle Detection API", version="2.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


# ── Models ─────────────────────────────────────────────────────────────────────
class DetectRequest(BaseModel):
    imageData: str
    confidence: float = 0.35


class Detection(BaseModel):
    label: str
    confidence: float
    points: float
    category: str
    bbox: list[float]


class DetectResponse(BaseModel):
    isBottle: bool
    detections: list[Detection]
    bestLabel: str
    bestConfidence: float
    totalPoints: float
    bottleSize: str
    bottleType: str
    pointsEarned: float


# ── Helpers ────────────────────────────────────────────────────────────────────
def decode_image(data: str) -> Image.Image:
    if "," in data:
        data = data.split(",", 1)[1]
    raw = base64.b64decode(data)
    img = Image.open(io.BytesIO(raw)).convert("RGB")

    # Enhance webcam images for better detection
    # 1. Resize to standard size if too small
    w, h = img.size
    if w < 320 or h < 320:
        scale = max(320 / w, 320 / h)
        img = img.resize((int(w * scale), int(h * scale)), Image.LANCZOS)

    # 2. Slightly sharpen to compensate for webcam blur
    from PIL import ImageFilter, ImageEnhance
    img = ImageEnhance.Sharpness(img).enhance(1.5)

    return img


def run_inference(image: Image.Image) -> list[Detection]:
    """
    Try each confidence level. If nothing found, try with
    brightness/sharpness variations to handle webcam quality issues.
    """
    # Pass 1: image as-is
    detections = _run_at_thresholds(image)
    if detections:
        return detections

    # Pass 2: brighter version (helps with dim webcam)
    from PIL import ImageEnhance
    brighter = ImageEnhance.Brightness(image).enhance(1.4)
    detections = _run_at_thresholds(brighter)
    if detections:
        print("[YOLO] Detected on brighter pass")
        return detections

    # Pass 3: more contrast
    contrast = ImageEnhance.Contrast(image).enhance(1.5)
    detections = _run_at_thresholds(contrast)
    if detections:
        print("[YOLO] Detected on contrast pass")
        return detections

    print("[YOLO] No detections on any pass.")
    return []


def _run_at_thresholds(image: Image.Image) -> list[Detection]:
    """Try each confidence level until at least one box is found."""
    for threshold in CONF_LADDER:
        results    = model(image, conf=threshold, verbose=False)
        detections = []
        for result in results:
            if result.boxes is None:
                continue
            h, w = result.orig_shape
            for box in result.boxes:
                cls_id   = int(box.cls[0])
                label    = model.names[cls_id]
                conf_val = float(box.conf[0])
                pts      = POINTS_MAP.get(label, 1.0)
                cat      = CATEGORY_MAP.get(label, "medium")
                x1, y1, x2, y2 = box.xyxy[0].tolist()
                detections.append(Detection(
                    label=label,
                    confidence=round(conf_val, 4),
                    points=pts,
                    category=cat,
                    bbox=[
                        round(x1 / w, 4), round(y1 / h, 4),
                        round(x2 / w, 4), round(y2 / h, 4),
                    ],
                ))
        if detections:
            print(f"[YOLO] Detected {len(detections)} box(es) at conf>={threshold}: "
                  f"{[f'{d.label}:{d.confidence}' for d in detections]}")
            return detections

    print("[YOLO] No detections at any threshold.")
    return []


# ── Endpoints ──────────────────────────────────────────────────────────────────
@app.get("/health")
def health():
    return {"status": "ok", "model": MODEL_PATH.name, "classes": model.names}


@app.post("/detect", response_model=DetectResponse)
def detect(req: DetectRequest):
    try:
        image = decode_image(req.imageData)
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid image: {e}")

    detections = run_inference(image)

    if not detections:
        return DetectResponse(
            isBottle=False,
            detections=[],
            bestLabel="none",
            bestConfidence=0.0,
            totalPoints=0.0,
            bottleSize="medium",
            bottleType="No bottle detected",
            pointsEarned=0.0,
        )

    best       = max(detections, key=lambda d: d.confidence)
    total_pts  = sum(d.points for d in detections)
    earned     = max(1.0, round(total_pts))

    return DetectResponse(
        isBottle=True,
        detections=detections,
        bestLabel=best.label,
        bestConfidence=best.confidence,
        totalPoints=total_pts,
        bottleSize=best.category,
        bottleType=f"{best.label} PET Plastic Bottle",
        pointsEarned=earned,
    )


# ── Entry point ────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run(app, host="0.0.0.0", port=port, reload=False)
