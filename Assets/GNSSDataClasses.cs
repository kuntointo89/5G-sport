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
public class Root {
    public List<Player> players;
}