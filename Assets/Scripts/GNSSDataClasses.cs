using System.Collections.Generic;
using System.Numerics;
using System;

[System.Serializable]
public class Vector3Serializable
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class GNSSData
{
    public string Pico_ID;
    public string GNSS_ID;
    public string Date;
    public double Latitude;
    public double Longitude;
    public double Timestamp_UTC;
    public long Timestamp_ms;

    public DateTime GetDateTimeUTC()
    {
        return DateTimeOffset.FromUnixTimeSeconds((long)Timestamp_UTC).UtcDateTime;
    }
}

[System.Serializable]
public class HeartRateData
{
    public List<int> rrData;
    public string Pico_ID;
    public long Movesense_series;
    public int Timestamp_UTC;
    public long Timestamp_ms;
    public float average_bpm;
}

[System.Serializable]
public class IMUData
{
    public string Pico_ID;
    public long Movesense_series;
    public double Timestamp_UTC;
    public long Timestamp_ms;
    public List<Vector3Serializable> ArrayAcc;
    public List<Vector3Serializable> ArrayGyro;
    public List<Vector3Serializable> ArrayMagn;
}

[System.Serializable]
public class ECGData
{
    public string Pico_ID;
    public List<int> Samples;
    public int Movesense_series;
    public double Timestamp_UTC;
    public long Timestamp_ms;
}

[System.Serializable]
public class PlayerPayload
{
    public string playerId;
    public string type;
    public GNSSData GNSS;
    public HeartRateData Heart_rate;
    public IMUData IMU;
    public ECGData ECG;
}

[System.Serializable]
public class PlayerMessageWrapper
{
    public List<PlayerPayload> players;
}

[System.Serializable]
public class PlayerAPIResponseList
{
    public List<PlayerPayload> players;
}

