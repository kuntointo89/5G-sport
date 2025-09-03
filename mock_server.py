from fastapi import FastAPI, Query
from fastapi.responses import JSONResponse
import asyncio, websockets, json, random, time
from threading import Thread
from datetime import datetime
import uvicorn

NUM_PLAYERS = 10
history_data = {
    "gnss": [],
    "heart_rate": [],
    "imu": [],
    "ecg": []
}

ts_counter = 0
ts_lock = asyncio.Lock()  # to synchronize timestamp updates

def generate_vector3():
    return {"x": round(random.uniform(-3, 3), 2),
            "y": round(random.uniform(-3, 3), 2),
            "z": round(random.uniform(-3, 3), 2)}

current_hr = {player_id: random.randint(125, 180) for player_id in range(1, NUM_PLAYERS + 1)}

# --- Data generators ---
def generate_gnss(player_id, ts):
    ts_val = ts * 1000
    return {
        "Pico_ID": f"Pico_{player_id}",
        "GNSS_ID": f"GNSS_{player_id}",
        "Date": datetime.utcnow().strftime("%d.%m.%Y").lstrip("0").replace(".0", "."),
        "Latitude": round(random.uniform(40.0, 41.0), 6),
        "Longitude": round(random.uniform(-74.0, -73.0), 6),
        "Timestamp_UTC": ts_val,
        "Timestamp_ms": ts_val
    }

def generate_heartrate(player_id, ts):
    ts_val = ts * 1000
    delta = random.uniform(-3, 3)
    hr = current_hr[player_id] + delta
    hr = max(125, min(180, hr))
    current_hr[player_id] = hr
    avg_rr = 60000 / hr
    rr_data = [int(random.gauss(avg_rr, 5)) for _ in range(5)]
    return {
        "rrData": rr_data,
        "Pico_ID": f"Pico_{player_id}",
        "Movesense_series": random.randint(1, 10000),
        "Timestamp_ms": ts_val,
        "average_bpm": round(hr, 2)
    }

def generate_imu(player_id, ts):
    ts_val = ts * 1000
    return {
        "yaw": round(random.uniform(0, 360), 2),
        "Pico_ID": f"Pico_{player_id}",
        "Movesense_series": random.randint(1, 10000),
        "Timestamp_ms": ts_val,
        "ArrayAcc": [generate_vector3() for _ in range(5)],
        "ArrayGyro": [generate_vector3() for _ in range(5)],
        "ArrayMagn": [generate_vector3() for _ in range(5)]
    }

def generate_ecg(player_id, ts):
    return {
        "Pico_ID": f"Pico_{player_id}",
        "Samples": [random.randint(-1000, 1000) for _ in range(10)],
        "Movesense_series": random.randint(1, 10000),
        "Timestamp_ms": ts * 1000
    }

def generate_player_data(player_id, data_type, ts):
    if data_type == "gnss": return generate_gnss(player_id, ts)
    if data_type == "heart_rate": return generate_heartrate(player_id, ts)
    if data_type == "imu": return generate_imu(player_id, ts)
    if data_type == "ecg": return generate_ecg(player_id, ts)

# --- Background data generation ---
async def generate_data_forever():
    global ts_counter
    while True:
        async with ts_lock:
            ts_counter += 1
            current_ts = ts_counter

        for player_id in range(1, NUM_PLAYERS + 1):
            for data_type in ["gnss", "heart_rate", "imu", "ecg"]:
                data = generate_player_data(player_id, data_type, current_ts)
                history_data[data_type].append(data)
                if len(history_data[data_type]) > 1000:
                    history_data[data_type].pop(0)
        await asyncio.sleep(1)

# --- WebSocket server ---
async def websocket_handler(websocket):
    global ts_counter
    try:
        while True:
            async with ts_lock:
                ts_counter += 1
                current_ts = ts_counter

            players_payload = []
            for player_id in range(1, NUM_PLAYERS + 1):
                player_data = {}
                for data_type in ["gnss", "heart_rate", "imu", "ecg"]:
                    data = generate_player_data(player_id, data_type, current_ts)
                    history_data[data_type].append(data)
                    if len(history_data[data_type]) > 1000:
                        history_data[data_type].pop(0)
                    player_data[data_type] = data

                players_payload.append({
                    "playerId": player_id,
                    "type": "full_payload",
                    **player_data
                })

            await websocket.send(json.dumps({"players": players_payload}))
            await asyncio.sleep(1)
    except websockets.ConnectionClosed:
        print("Client disconnected")

async def start_ws_server():
    async with websockets.serve(websocket_handler, "0.0.0.0", 8765):
        print("WebSocket server started on ws://0.0.0.0:8765")
        await asyncio.Future()  # run forever

def start_websocket_server():
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    loop.run_until_complete(start_ws_server())

Thread(target=start_websocket_server, daemon=True).start()

# --- FastAPI app ---
app = FastAPI()

@app.on_event("startup")
async def start_background_tasks():
    asyncio.create_task(generate_data_forever())

@app.get("/api/{data_type}")
async def get_data(
    data_type: str,
    start_timestamp: int = Query(None),
    end_timestamp: int = Query(None),
    player_id: str = Query(None)
):
    try:
        data_type = data_type.lower()
        if data_type not in ["gnss", "heart_rate", "imu", "ecg"]:
            return JSONResponse({"error": "Invalid data type"}, status_code=400)

        player_ids = None
        if player_id:
            try:
                player_ids = [int(p.strip()) for p in player_id.split(",")]
            except ValueError:
                return JSONResponse({"error": "Invalid player_id format"}, status_code=400)

        if start_timestamp is None or end_timestamp is None:
            filtered = [
                d for d in history_data[data_type]
                if (player_ids is None or int(d["Pico_ID"].split("_")[1]) in player_ids)
            ]
        else:
            start_ms = start_timestamp * 1000
            end_ms = end_timestamp * 1000
            filtered = [
                d for d in history_data[data_type]
                if start_ms <= d["Timestamp_ms"] <= end_ms
                and (player_ids is None or int(d["Pico_ID"].split("_")[1]) in player_ids)
            ]

        if not filtered:
            return JSONResponse({"error": f"No {data_type.upper()} data found"}, status_code=404)

        if data_type in ["gnss", "heart_rate"]:
            latest = max(filtered, key=lambda x: x["Timestamp_ms"])
            response_data = latest.copy()
            if data_type == "heart_rate":
                response_data["Pico_ID"] = int(latest["Pico_ID"].split("_")[1])
                response_data["Timestamp_UTC"] = latest["Timestamp_ms"]
            return JSONResponse(response_data)

        return JSONResponse({"players": filtered})

    except Exception as e:
        import traceback
        traceback.print_exc()
        return JSONResponse({"error": str(e)}, status_code=500)

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=5000)
