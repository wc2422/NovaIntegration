import os
import sys
from pathlib import Path

import torch
from ultralytics import YOLO


AI_DIR = Path(__file__).resolve().parent
RUNS_DIR = AI_DIR / "runs" / "detect"
PREFERRED_MODEL = RUNS_DIR / "security-gpu-light" / "weights" / "best.pt"


def main():
    os.chdir(AI_DIR)
    RUNS_DIR.mkdir(parents=True, exist_ok=True)

    model_path = PREFERRED_MODEL
    image_path = AI_DIR / "busystreet.jpg"
    device = 0 if torch.cuda.is_available() else "cpu"

    print(f"Model: {model_path}")
    print(f"Image: {image_path}")
    print(f"Device: {device}")

    model = YOLO(str(model_path))
    results=model.predict(
        source=str(image_path),
        project=str(RUNS_DIR),
        name="predict-test",
        exist_ok=True,
        conf=0.25,
        device=device,
    )

    for result in results:
        print(result.boxes)
        print(result.names)

    print(model.names)

if __name__ == "__main__":
    main()
