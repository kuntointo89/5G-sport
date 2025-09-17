using System;

[System.Serializable]
public class PlayerAPIResponse
{
    public string playerId;
    public GNSSData GNSS;
    public HeartRateData Heart_rate;
    public IMUData IMU;
    public ECGData ECG;
    public string timestamp; // milliseconds since Unix epoch as string

    public static PlayerAPIResponse FromPayload(PlayerPayload payload)
    {
        return new PlayerAPIResponse
        {
            playerId = payload.playerId,
            GNSS = payload.GNSS,
            Heart_rate = payload.Heart_rate,
            IMU = payload.IMU,
            ECG = payload.ECG,
            timestamp = payload.GNSS != null 
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)payload.GNSS.Timestamp_UTC).ToUnixTimeMilliseconds().ToString() 
                : "0"
        };
    }
}


