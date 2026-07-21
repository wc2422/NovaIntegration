#include <opencv2/opencv.hpp>
#include <iostream>
#include <string>
#include <chrono> 
#include <windows.h>  
#include <sqlext.h>   

using namespace cv;
using namespace std;


void LogAlertToOracle(string objectClass, float confidenceScore) {
    SQLHENV hEnv;
    SQLHDBC hDbc;
    SQLHSTMT hStmt;
    SQLRETURN ret;

    SQLAllocHandle(SQL_HANDLE_ENV, SQL_NULL_HANDLE, &hEnv);
    SQLSetEnvAttr(hEnv, SQL_ATTR_ODBC_VERSION, (void*)SQL_OV_ODBC3, 0);
    SQLAllocHandle(SQL_HANDLE_DBC, hEnv, &hDbc);

    SQLCHAR outstr[1024];
    SQLSMALLINT outstrlen;

    
    ret = SQLDriverConnectA(hDbc, NULL,
        (SQLCHAR*)"DSN=OracleDB;UID=system;PWD=Sheridan2026;",
        SQL_NTS, outstr, sizeof(outstr), &outstrlen, SQL_DRIVER_NOPROMPT);

    if (SQL_SUCCEEDED(ret)) {
        cout << "Successfully connected to Oracle Database!" << endl;
        SQLAllocHandle(SQL_HANDLE_STMT, hDbc, &hStmt);

        
        string sqlQuery = "BEGIN log_new_alert('" + objectClass + "', " + to_string(confidenceScore) + "); END;";

        ret = SQLExecDirectA(hStmt, (SQLCHAR*)sqlQuery.c_str(), SQL_NTS);

        if (SQL_SUCCEEDED(ret)) {
            cout << "Alert successfully logged to the security_alerts table." << endl;
        }
        else {
            cout << "ERROR: Failed to execute the PL/SQL procedure." << endl;
        }

        SQLFreeHandle(SQL_HANDLE_STMT, hStmt);
        SQLDisconnect(hDbc);
    }
    else {
        cout << "ERROR: Failed to connect to Oracle." << endl;
    }

    SQLFreeHandle(SQL_HANDLE_DBC, hDbc);
    SQLFreeHandle(SQL_HANDLE_ENV, hEnv);
}

int main() {
    VideoCapture cap(0); 

    if (!cap.isOpened()) {
        cout << "Error: Could not open the live RTSP stream." << endl;
        return -1;
    }

    cout << "Live stream successfully opened. Extracting frames... Press 'q' to quit." << endl;


    

    Mat frame;
    int frameCount = 0;

   

    while (true) {
        cap.grab();
        frameCount++;

        if (frameCount % 30 == 0) {
            cap.retrieve(frame);

            if (frame.empty()) {
                cout << "Stream disconnected or empty frame captured." << endl;
                break;
            }

            resize(frame, frame, Size(1280, 720));

            auto now = chrono::system_clock::now();
            auto timestamp = chrono::duration_cast<chrono::milliseconds>(now.time_since_epoch()).count();

            string filename = "C:\\Capstone\\SharedBuffer\\frame_" + to_string(timestamp) + ".jpg";
            bool isSaved = imwrite(filename, frame);

            if (isSaved) {
                cout << "Successfully saved: " << filename << endl;

               
            }
            else {
                cout << "FAILED TO SAVE: Does the folder C:\\Capstone\\SharedBuffer\\ exist?" << endl;
            }

            imshow("Edge AI Capstone - Live Camera Feed Test", frame);
        }

        if (waitKey(1) == 'q') {
            cout << "User pressed 'q'. Exiting..." << endl;
            break;
        }
    }

    cap.release(); 
    destroyAllWindows();
    return 0;
}