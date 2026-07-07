from pathlib import Path
from typing import Any
from PIL import Image
import io
import cv2
import numpy as np
import torch
from fastapi import FastAPI, File, HTTPException, UploadFile
from ultralytics import YOLO
from fastapi.responses import Response

BASE_DIR = Path(__file__).resolve().parent
MODEL_PATH = BASE_DIR / "runs" / "detect" / "security-gpu-light" / "weights" / "best.pt" 


app = FastAPI(title="Nova YOLO Analysis API")

device: int | str = 0 if torch.cuda.is_available() else "cpu"
model_path = MODEL_PATH
model = YOLO(str(model_path))


def class_name_for(class_id: int) -> str:
    return model.names.get(class_id, str(class_id))


@app.post("/analyze-frame")
async def analyze_frame(file: UploadFile = File(...)) -> Response:
    if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="File must be an image")
    try:
        contents = await file.read()
        image = Image.open(io.BytesIO(contents)).convert("RGB")
        prediction=model.predict(
        source=image,
        conf=0.25,
        device=device
        )
        final_image = prediction[0].plot()
        encoded_image = cv2.imencode(".jpg", final_image)
        

        return Response(
            content=encoded_image[1].tobytes(),
            media_type="image/jpeg",
        )
    except Exception as e:
        return {"error": f"Failed to process image {e}"}
    finally:
        await file.close()
