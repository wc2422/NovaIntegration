


INSERT INTO CAMERAS (CameraID, IP_Address, Location_Name, Status) 
VALUES (205, '192.168.1.45', 'West Parking Lot Gate', 'Active');


INSERT INTO USERS (User_ID, Username, Password_Hash, Role) 
VALUES (1001, 'property_mgr_val', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'Administrator');


INSERT INTO BOUNDARIES (Boundary_ID, Camera_ID, Boundary_Type, Coordinates) 
VALUES (3012, 205, 'Line', '[(200, 450), (750, 450)]');


INSERT INTO SCHEDULES (Schedule_ID, Camera_ID, Start_Time, End_Time, Is_Active) 
VALUES (401, 205, '18:00:00', '06:00:00', 'True');


INSERT INTO EVENTS (Event_ID, Camera_ID, Timestamp, Alert_Type, Object_Class, Confidence_Score, Video_Clip_Path) 
VALUES (9044, 205, TO_TIMESTAMP('2026-06-04 10:02:00', 'YYYY-MM-DD HH24:MI:SS'), 'Boundary_Cross', 'Vehicle', 94.75, '/local_storage/clips/evt_9044.mp4');


COMMIT;