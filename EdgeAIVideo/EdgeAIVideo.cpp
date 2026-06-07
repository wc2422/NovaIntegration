#include <opencv2/opencv.hpp>
#include <iostream>

using namespace cv;
using namespace std;

int main() {
    // Open the default camera (index 0)
    VideoCapture cap("parking_lot.mp4");

    if (!cap.isOpened()) {
        cout << "Error: Could not open the camera." << endl;
        return -1;
    }

    cout << "Camera successfully opened. Press 'q' to quit." << endl;

    Mat frame;
    while (true) {
        // Captures a new frame from the camera
        cap >> frame;

        if (frame.empty()) {
            cout << "Error: Captured empty frame." << endl;
            break;
        }

        // Display the frame in a window
        imshow("Edge AI Capstone - Camera Feed Test", frame);

        // Wait for 1 millisecond and check if 'q' was pressed
        if (waitKey(1) == 'q') {
            break;
        }
    }

    // Clean up
    cap.release();
    destroyAllWindows();

    return 0;
}