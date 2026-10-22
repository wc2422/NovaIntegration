#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include <filesystem>
#include <opencv2/opencv.hpp>
#include <iostream>
#include <string>
#include <chrono>
#include <winsock2.h> 
#include <windows.h>
#include <sqlext.h>
#include <cpr/cpr.h>
#include <nlohmann/json.hpp>
#include <thread>
#include <future>
#include <mutex>
#include <queue>
#include <condition_variable>
#include <limits> 

#pragma comment(lib, "ws2_32.lib") 

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
    HANDLE hInput = GetStdHandle(STD_INPUT_HANDLE);
    DWORD prev_mode;
    GetConsoleMode(hInput, &prev_mode);
    SetConsoleMode(hInput, ENABLE_EXTENDED_FLAGS | (prev_mode & ~ENABLE_QUICK_EDIT_MODE));

    std::cout << "Booting up the local AI server in the background..." << std::endl;
    system("start /MIN powershell -Command \"cd C:\\Users\\Valmi\\OneDrive\\Documents\\Dll\\EdgeAIVideo; $venv = Join-Path $env:LOCALAPPDATA 'NovaIntegration\\ai-venv'; $python = Join-Path $venv 'Scripts\\python.exe'; & $python -m uvicorn AIendpoint:app --app-dir .\\AI --host 127.0.0.1 --port 8000\"");

    Sleep(3000);

    string streamUrl;
    std::cout << "Camera Setup: Press Enter for physical camera, or '0' for webcam:\n";
    getline(cin, streamUrl);

    if (streamUrl.empty()) {
        streamUrl = "rtsp://66.129.154.87:22022";
        std::cout << "[Config] Using physical camera stream: " << streamUrl << std::endl;
    }
    else if (streamUrl == "0") {
        std::cout << "[Config] Using local webcam (ID: 0)" << std::endl;
    }

    thread consumer(ConsumerThread);
    consumer.detach();

    while (true) {
        VideoCapture cap;
        bool openSuccess = false;

        if (streamUrl == "0") {
            openSuccess = cap.open(0, CAP_DSHOW);
        }
        else {
            std::cout << "[System] Pinging physical camera (5 second window)..." << std::endl;

            auto futureCheck = std::async(std::launch::async, []() {
                WSADATA wsaData;
                WSAStartup(MAKEWORD(2, 2), &wsaData);
                SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);

                u_long iMode = 1;
                ioctlsocket(sock, FIONBIO, &iMode);

                sockaddr_in serverAddr;
                serverAddr.sin_family = AF_INET;
                serverAddr.sin_port = htons(22022);
                serverAddr.sin_addr.s_addr = inet_addr("66.129.154.87");

                connect(sock, (struct sockaddr*)&serverAddr, sizeof(serverAddr));

                fd_set set;
                FD_ZERO(&set);
                FD_SET(sock, &set);
                timeval timeout;
                timeout.tv_sec = 5;
                timeout.tv_usec = 0;

                int rv = select(0, NULL, &set, NULL, &timeout);
                closesocket(sock);
                WSACleanup();
                return rv > 0;
                });

            if (futureCheck.wait_for(std::chrono::seconds(5)) == std::future_status::timeout) {
                std::cout << "[Timeout] Physical camera did not respond within 5 seconds." << std::endl;
                openSuccess = false;
            }
            else {
                bool reachable = futureCheck.get();
                if (reachable) {
                    _putenv_s("OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp");
                    openSuccess = cap.open(streamUrl, CAP_FFMPEG);
                }
                else {
                    openSuccess = false;
                }
            }
        }

        if (!openSuccess) {
            if (streamUrl != "0") {
                std::cout << "[Fallback] Auto-switching to local webcam immediately..." << std::endl;
                streamUrl = "0";
                continue;
            }
            else {
                std::cout << "\n[Critical] Local webcam failed to open. Retrying in 5 seconds..." << std::endl;
                Sleep(5000);
                continue;
            }
        }

        namedWindow("Edge AI Capstone", WINDOW_NORMAL);
        std::cout << "[Connected] Video stream successfully opened." << std::endl;
        std::cout << "Shortcuts: Press 'q' to quit, '0' to switch to webcam, 'A' to switch to physical cam." << std::endl;

        auto lastSentTime = chrono::system_clock::now();
        const int sendIntervalMs = 1000;
        bool userQuit = false;

        while (true) {
            Mat frame;
            cap >> frame;

            if (frame.empty()) {
                std::cout << "\n[Warning] Video stream disconnected. Falling back to webcam..." << std::endl;
                streamUrl = "0";
                break;
            }

            resize(frame, frame, Size(1280, 720));

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
            else if (key == '0') {
                std::cout << "[Manual Override] Switching to local webcam..." << std::endl;
                streamUrl = "0";
                break;
            }
            else if (key == 'a' || key == 'A') {
                std::cout << "[Manual Override] Switching to physical camera..." << std::endl;
                streamUrl = "rtsp://66.129.154.87:22022";
                break;
            }
        }

        cap.release();
        cv::destroyAllWindows();

        if (userQuit) {
            break;
        }
    }

    return 0;
}