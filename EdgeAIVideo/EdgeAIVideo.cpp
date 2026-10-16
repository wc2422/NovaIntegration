#include <filesystem>
#include <opencv2/opencv.hpp>
#include <iostream>
#include <string>
#include <chrono>
#include <windows.h>
#include <sqlext.h>
#include <cpr/cpr.h>
#include <nlohmann/json.hpp>
#include <thread>
#include <future>
#include <mutex>
#include <queue>
#include <condition_variable>

using json = nlohmann::json;
using namespace cv;
using namespace std;
namespace fs = std::filesystem;

mutex frameMutex;
vector<json> latestDetections;

queue<Mat> frameQueue;
mutex queueMutex;
condition_variable queueCondVar;
const int MAX_QUEUE_SIZE = 5;




const string STORAGE_DIR = "D:\\Capstone\\SharedBuffer\\";



void LogAlertsToOracle(const json& detections) {
    if (detections.empty()) return;

    HENV hEnv;
    HDBC hDbc;
    HSTMT hStmt;
    SQLRETURN ret;

    SQLAllocHandle(SQL_HANDLE_ENV, SQL_NULL_HANDLE, &hEnv);
    SQLSetEnvAttr(hEnv, SQL_ATTR_ODBC_VERSION, (void*)SQL_OV_ODBC3, 0);
    SQLAllocHandle(SQL_HANDLE_DBC, hEnv, &hDbc);
    SQLSetConnectAttr(hDbc, SQL_ATTR_AUTOCOMMIT, (SQLPOINTER)SQL_AUTOCOMMIT_ON, 0);

    SQLCHAR outstr[1024];
    SQLSMALLINT outstrlen;

    ret = SQLDriverConnectA(hDbc, NULL, (SQLCHAR*)"Driver={Oracle in OraDB21Home1};DBQ=localhost:1521/XE;UID=system;PWD=Sheridan2026;", SQL_NTS, outstr, (SQLSMALLINT)sizeof(outstr), &outstrlen, SQL_DRIVER_NOPROMPT);

    if (SQL_SUCCEEDED(ret)) {
        SQLAllocHandle(SQL_HANDLE_STMT, hDbc, &hStmt);

        for (const auto& item : detections) {
            string objectClass = item["class_name"];
            float confidenceScore = item["confidence"];
            confidenceScore *= 100.0f; 

            string query = "BEGIN "
                "INSERT INTO system.EVENTS (Camera_ID, Alert_Type, Object_Class, Confidence_Score) "
                "VALUES (205, 'AI_Detection', '" + objectClass + "', " + to_string(confidenceScore) + "); "
                "COMMIT; "
                "END;";

            ret = SQLExecDirectA(hStmt, (SQLCHAR*)query.c_str(), SQL_NTS);

            if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO) {
                std::cout << "[Oracle DB] Alert logged permanently for: " << objectClass << std::endl;
            }
            else {
                std::cerr << "[Oracle DB Error] Insert failed for: " << objectClass << std::endl;
            }
        }

        SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
        SQLDisconnect(hDbc);
    }
    else {
        std::cerr << "[Oracle DB Error] Database connection failed." << std::endl;
    }

    SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
    SQLFreeHandle(SQL_HANDLE_ENV, hEnv);
}

void SendFrameToAI(const string& imageFilePath) {
    cpr::Response r = cpr::Post(
        cpr::Url{ "http://127.0.0.1:8000/analyze-frame-json" },
        cpr::Multipart{ {"file", cpr::File{imageFilePath}} }
    );

    if (r.status_code == 200) {
        std::cout << "[HTTP 200] Frame sent successfully!" << std::endl;

        try {
            json responseJson = json::parse(r.text);

            {
                lock_guard<mutex> lock(frameMutex);
                latestDetections.clear();
                for (const auto& item : responseJson) {
                    latestDetections.push_back(item);
                }
            }

            
            LogAlertsToOracle(responseJson);
        }
        catch (const exception& e) {
            std::cerr << "[JSON Error] Parsing failed: " << e.what() << std::endl;
        }
    }
    else {
        std::cerr << "[HTTP Error] Failed to reach AI endpoint. Status Code: " << r.status_code << std::endl;
    }
}

void CleanupStorage() {
    int maxFramesToKeep = 50;
    vector<fs::path> frames;

    try {
       
        if (!fs::exists(STORAGE_DIR)) {
            return;
        }

        for (const auto& entry : fs::directory_iterator(STORAGE_DIR)) {
            if (entry.is_regular_file() && entry.path().extension() == ".jpg" && entry.path().filename() != "latest.jpg") {
                frames.push_back(entry.path());
            }
        }

        if (frames.size() > maxFramesToKeep) {
            sort(frames.begin(), frames.end(), [](const fs::path& a, const fs::path& b) {
                return fs::last_write_time(a) < fs::last_write_time(b);
                });

            int filesToDelete = frames.size() - maxFramesToKeep;
            for (int i = 0; i < filesToDelete; ++i) {
                fs::remove(frames[i]);
            }
            std::cout << "[Storage Cleanup] Removed " << filesToDelete << " old frames from secondary drive to save disk space." << std::endl;
        }
    }
    catch (const exception& e) {
        std::cerr << "[Storage Cleanup Error] " << e.what() << std::endl;
    }
}

void ConsumerThread() {
  
    try {
        if (!fs::exists(STORAGE_DIR)) {
            fs::create_directories(STORAGE_DIR);
            std::cout << "[Storage] Created secondary drive cache directory at " << STORAGE_DIR << std::endl;
        }
    }
    catch (const exception& e) {
        std::cerr << "[CRITICAL STORAGE ERROR] USB Drive not found: " << STORAGE_DIR << ". Error: " << e.what() << std::endl;
        return; 
    }

    while (true) {
        Mat frameToProcess;

        {
            unique_lock<mutex> lock(queueMutex);
            queueCondVar.wait(lock, [] { return !frameQueue.empty(); });
            frameToProcess = frameQueue.front();
            frameQueue.pop();
        }

        auto now = chrono::system_clock::now();
        auto timestamp = chrono::duration_cast<chrono::milliseconds>(now.time_since_epoch()).count();

       
        string filename = STORAGE_DIR + "frame_" + to_string(timestamp) + ".jpg";
        string latestFilename = STORAGE_DIR + "latest.jpg";

        imwrite(latestFilename, frameToProcess);
        if (imwrite(filename, frameToProcess)) {
            SendFrameToAI(filename);
            CleanupStorage();
        }
    }
}

int main() {
    std::cout << "Booting up the local AI server in the background..." << std::endl;
    system("start /MIN powershell -Command \"cd C:\\Users\\Valmi\\OneDrive\\Documents\\Dll\\EdgeAIVideo; $venv = Join-Path $env:LOCALAPPDATA 'NovaIntegration\\ai-venv'; $python = Join-Path $venv 'Scripts\\python.exe'; & $python -m uvicorn AIendpoint:app --app-dir .\\AI --host 127.0.0.1 --port 8000\"");

    Sleep(3000);

    thread consumer(ConsumerThread);
    consumer.detach();

    while (true) {
        // --- NETWORK STREAM (ALEX) ---
        // string rtspUrl = "rtsp://173.206.214.57:22022";
        // _putenv_s("OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp"); 
        // VideoCapture cap(rtspUrl, CAP_FFMPEG);

        VideoCapture cap(0, CAP_DSHOW);

        if (!cap.isOpened()) {
            std::cout << "[Warning] Could not open the video stream. Retrying in 5 seconds..." << std::endl;
            Sleep(5000);
            continue;
        }

        namedWindow("Edge AI Capstone", WINDOW_NORMAL);
        std::cout << "[Connected] Video stream successfully opened. Extracting frames... Press 'q' to quit" << std::endl;

        auto lastSentTime = chrono::system_clock::now();
        const int sendIntervalMs = 1000;
        bool userQuit = false;

        
        while (true) {
            Mat frame;
            cap >> frame;
            if (frame.empty()) {
                std::cout << "[Warning] Video stream disconnected. Attempting to reconnect..." << std::endl;
                break;
            }

            auto now = chrono::system_clock::now();

            
            if (chrono::duration_cast<chrono::milliseconds>(now - lastSentTime).count() > sendIntervalMs) {
                {
                    lock_guard<mutex> lock(queueMutex);
                    if (frameQueue.size() < MAX_QUEUE_SIZE) {
                        frameQueue.push(frame.clone());
                    }
                }
                queueCondVar.notify_one();
                lastSentTime = now;
            }

          
            {
                lock_guard<mutex> lock(frameMutex);
                for (const auto& item : latestDetections) {
                    string className = item["class_name"];
                    float confidence = item["confidence"];
                    int x_min = item["xyxy"][0];
                    int y_min = item["xyxy"][1];
                    int x_max = item["xyxy"][2];
                    int y_max = item["xyxy"][3];

                    rectangle(frame, Point(x_min, y_min), Point(x_max, y_max), Scalar(255, 0, 0), 2);
                    string label = className + " " + to_string((int)(confidence * 100)) + "%";
                    putText(frame, label, Point(x_min, y_min - 10), FONT_HERSHEY_SIMPLEX, 0.6, Scalar(255, 0, 0), 2);
                }
            }

            imshow("Edge AI Capstone", frame);

            char key = (char)waitKey(1);
            if (key == 'q' || key == 'Q') {
                std::cout << "User pressed 'q'. Exiting completely..." << std::endl;
                userQuit = true;
                break;
            }
        }

        cap.release();
        cv::destroyAllWindows();

        if (userQuit) {
            break;
        }

        Sleep(5000);
    }

    return 0;
}