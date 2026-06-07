#include <opencv2/opencv.hpp>
#include <iostream>

using namespace cv;
using namespace std;

int main() {
    
    VideoCapture cap("parking_lot.mp4");

    if (!cap.isOpened()) {
        cout << "Error: Could not open the camera." << endl;
        return -1;
    }

    cout << "Camera successfully opened. Press 'q' to quit." << endl;

    Mat frame;
    while (true) {
        
        cap >> frame;

        if (frame.empty()) {
            cout << "Error: Captured empty frame." << endl;
            break;
        }

     
        imshow("Edge AI Capstone - Camera Feed Test", frame);

        
        if (waitKey(1) == 'q') {
            break;
        }
    }

   
    cap.release();
    destroyAllWindows();

    return 0;
}