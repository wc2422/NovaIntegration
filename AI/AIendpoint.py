from pathlib import Path
from PIL import Image, UnidentifiedImageError
import io
import cv2
import torch
from fastapi import FastAPI, File, HTTPException, UploadFile
from ultralytics import YOLO
from fastapi.responses import Response
from pydantic import BaseModel


class PredictItem(BaseModel):
    class_name: str
    class_index: int
    confidence: float
    xyxy: list[float]

BASE_DIR = Path(__file__).resolve().parent
MODEL_PATH = BASE_DIR / "runs" / "detect" / "security-gpu-light" / "weights" / "best.pt" 


app = FastAPI(title="Nova YOLO Analysis API")

device: int | str = 0 if torch.cuda.is_available() else "cpu"
model_path = MODEL_PATH
model = YOLO(str(model_path))


def class_name_for(class_id: int) -> str:
    if isinstance(model.names, dict):
        return str(model.names.get(class_id, class_id))

    if 0 <= class_id < len(model.names):
        return str(model.names[class_id])

    return str(class_id)


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





@app.post("/analyze-frame-json", response_model=list[PredictItem])
async def analyze_frame_json(file: UploadFile = File(...)) -> list[PredictItem]:
    if file.content_type and not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="File must be an image")

    try:
        contents = await file.read()
        image = Image.open(io.BytesIO(contents)).convert("RGB")
        prediction = model.predict(
            source=image,
            conf=0.25,
            device=device,
            verbose=False,
        )
        bbox_list = prediction[0].boxes.xyxy.tolist()
        class_list = prediction[0].boxes.cls.tolist()
        confidence_list = prediction[0].boxes.conf.tolist()

        results: list[PredictItem] = []
        for box, class_value, confidence in zip(
            bbox_list,
            class_list,
            confidence_list,
        ):
            class_index = int(class_value)
            results.append(
                PredictItem(
                    class_name=class_name_for(class_index),
                    class_index=class_index,
                    confidence=round(float(confidence), 6),
                    xyxy=[round(float(coordinate), 2) for coordinate in box],
                )
            )
        return results
    except HTTPException:
        raise
    except Exception as exc:
        raise HTTPException(
            status_code=500,
            detail=f"Failed to process image: {exc}",
        ) from exc
    finally:
        await file.close()
