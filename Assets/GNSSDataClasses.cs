using System.Collections.Generic;
using System.Numerics;

// This script lists all data from the sensors and creates lists from them.

[System.Serializable]
public class GNSSPoint
{
    public string Date;       // Timestamp of the point
    public double Latitude;   // Latitude in decimal degrees
    public double Longitude;  // Longitude in decimal degrees
}

[System.Serializable]
public class IMU9
{
    public List<Vector3> ArrayAcc;   // Accelerometer data
    public List<Vector3> ArrayGyro;  // Gyroscope data
    public List<Vector3> ArrayMagn;  // Magnetometer data
}

[System.Serializable]
public class HRData
{
    public List<int> rrData;   // RR interval data (milliseconds)
    public float average;      // Average heart rate (bpm)
}

[System.Serializable]
public class ECGData
{
    public List<int> Samples;  // Raw ECG signal samples
}

[System.Serializable]
public class Sensor
{
    public int sensorId;          // Sensor unique identifier
    public HRData HR;             // Heart rate data
    public IMU9 IMU9;             // // IMU data: typically includes acceleration (m/s²), angular velocity (rad/s), and orientation (quaternion or Euler angles)
    public List<GNSSPoint> GNSS;  // Position data
    public ECGData ECG; // Electrocardiogram data (raw ECG signal samples)
}

[System.Serializable]
public class Player
{
    public int playerId;          // Player unique identifier
    public int sensorSetId;       // Sensor set ID
    public List<Sensor> sensors;  // List of sensors attached to player
}

[System.Serializable]
public class Root
{
    public List<Player> players;  // Root list of players from JSON
}
