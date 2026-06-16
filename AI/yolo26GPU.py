import os
from pathlib import Path

from ultralytics import YOLO


AI_DIR = Path(__file__).resolve().parent
RUNS_DIR = AI_DIR / "runs" / "detect"


def main():
    os.chdir(AI_DIR)
    RUNS_DIR.mkdir(parents=True, exist_ok=True)

    model = YOLO("yolo26n.pt")
    model.train(
        data="coco8.yaml",
        epochs=50,
        imgsz=640,
        batch=4,
        device=0,
        workers=0,
        cache=False,
        project=str(RUNS_DIR),
        name="security-gpu-light",
        patience=15,
        cos_lr=True,
        amp=True,
        plots=True,
    )


if __name__ == "__main__":
    main()
