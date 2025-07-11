using System.Collections.Generic;
using System.Numerics;

[System.Serializable]
public class GNSSPoint {
    public string Date;
    public double Latitude;
    public double Longitude;
}

[System.Serializable]
public class IMU9 {
    public List<Vector3> ArrayAcc;
    public List<Vector3> ArrayGyro;
    public List<Vector3> ArrayMagn;
}

[System.Serializable]
public class HRData
{
    public List<int> rrData;
    public float average;
    public float endurance;
}

[System.Serializable]
public class ECGData {
    public List<int> Samples;
}

[System.Serializable]
public class Sensor
{
    public int sensorId;
    public HRData HR;
    public IMU9 IMU9;
    public List<GNSSPoint> GNSS;
    public ECGData ECG;
}

[System.Serializable]
public class Player {
    public int playerId;
    public int sensorSetId;
    public List<Sensor> sensors;
}

[System.Serializable]
public class Root
{
    public List<Player> players;
}

[System.Serializable]
public class RealtimePlayerUpdate
{
    public string playerId;
    public double latitude;
    public double longitude;
    public int [] ecgSample;
    public int hrValue;
    public double timestamp;
}

[System.Serializable]
public class PlayerUpdateWrapper
{
    public List<RealtimePlayerUpdate> players;
}

public static class GlobalGPSOrigin
{
    public static double baseLat = 0;
    public static double baseLon = 0;
    public static bool Initialized = false;
}
