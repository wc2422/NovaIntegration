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

using json = nlohmann::json;
using namespace cv;
using namespace std;

mutex frameMutex;
vector<json> latestDetections;

void LogAlertToOracle(string objectClass, float confidenceScore) {
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


        string query = "BEGIN "
            "INSERT INTO system.EVENTS (Camera_ID, Alert_Type, Object_Class, Confidence_Score) "
            "VALUES (205, 'AI_Detection', '" + objectClass + "', " + to_string(confidenceScore) + "); "
            "COMMIT; "
            "END;";

        ret = SQLExecDirectA(hStmt, (SQLCHAR*)query.c_str(), SQL_NTS);

        if (ret == SQL_SUCCESS || ret == SQL_SUCCESS_WITH_INFO) {
            cout << "[Oracle DB] Alert logged permanently for: " << objectClass << endl;
        }
        else {
            cerr << "[Oracle DB Error] Insert failed." << endl;
            SQLCHAR sqlState[6], msg[256];
            SQLINTEGER nativeError;
            SQLSMALLINT msgLen;
            SQLGetDiagRecA(SQL_HANDLE_STMT, hStmt, 1, sqlState, &nativeError, msg, sizeof(msg), &msgLen);
            cerr << "Error Details: " << msg << endl;
        }

        SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
        SQLDisconnect(hDbc);
    }
    else {
        cerr << "[Oracle DB Error] Database connection failed." << endl;
    }

    SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
    SQLFreeHandle(SQL_HANDLE_ENV, hEnv);
}

void SendFrameToAI(const string& imageFilePath) {
    cpr::Response r = cpr::Post(
        cpr::Url{ "https://untitled-cartel-sinuous.ngrok-free.dev/analyze-frame-json" },
        cpr::Multipart{ {"file", cpr::File{imageFilePath}} }
    );

    if (r.status_code == 200) {
        cout << "[HTTP 200] Frame sent successfully!" << endl;

        try {
            json responseJson = json::parse(r.text);

            lock_guard<mutex> lock(frameMutex);
            latestDetections.clear();

            for (const auto& item : responseJson) {
                latestDetections.push_back(item);
                string className = item["class_name"];
                float confidence = item["confidence"];

                LogAlertToOracle(className, confidence * 100.0f);
            }
        }
        catch (const exception& e) {
            cerr << "[JSON Error] Parsing failed: " << e.what() << endl;
        }
    }
    else {
        cerr << "[HTTP Error] Failed to reach AI endpoint. Status Code: " << r.status_code << endl;
    }
}

int main() {
    VideoCapture cap(0, CAP_DSHOW);
    if (!cap.isOpened()) {
        cout << "Error opening local webcam" << endl;
        return -1;
    }

    cout << "Webcam successfully opened. Extracting frames... Press 'q' to quit" << endl;

    auto lastSentTime = chrono::system_clock::now();
    const int sendIntervalMs = 1000;

    while (true) {
        Mat frame;
        cap >> frame;
        if (frame.empty()) {
            cout << "Webcam disconnected or empty frame captured." << endl;
            break;
        }

        auto now = chrono::system_clock::now();
        auto timestamp = chrono::duration_cast<chrono::milliseconds>(now.time_since_epoch()).count();

        if (chrono::duration_cast<chrono::milliseconds>(now - lastSentTime).count() > sendIntervalMs) {
            string filename = "C:\\Capstone\\SharedBuffer\\frame_" + to_string(timestamp) + ".jpg";
            bool isSaved = imwrite(filename, frame);


            imwrite("C:\\Capstone\\SharedBuffer\\latest.jpg", frame);

            if (isSaved) {
                cout << "Successfully saved: " << filename << endl;
                thread(SendFrameToAI, filename).detach();
            }
            else {
                cout << "FAILED TO SAVE: Does the folder C:\\Capstone\\SharedBuffer\\ exist?" << endl;
            }
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
            cout << "User pressed 'q'. Exiting..." << endl;
            break;
        }
    }

    cap.release();
    destroyAllWindows();
    return 0;
}