import asyncio
import websockets
import json
import random
import math
import datetime

PORT = 8765
FPS = 30
NUM_PLAYERS = 10

BASE_LAT = 0
BASE_LON = 0

# Rink bounds in meters
rink_min_x, rink_min_y = -30, -15
rink_max_x, rink_max_y = 30, 15

def meters_to_lat(meters):
    return meters / 111_320

def meters_to_lon(meters, lat_deg):
    return meters / (40075000 * math.cos(lat_deg * math.pi / 180) / 360.0)

rink_min_lat = meters_to_lat(rink_min_y)
rink_max_lat = meters_to_lat(rink_max_y)
rink_min_lon = meters_to_lon(rink_min_x, BASE_LAT)
rink_max_lon = meters_to_lon(rink_max_x, BASE_LAT)

def clamp(value, min_val, max_val):
    return max(min_val, min(value, max_val))

# Initialize players with roles and patrol targets
players = {}
for i in range(NUM_PLAYERS):
    if i < 3:
        role = "defender"
    elif i < 7:
        role = "midfielder"
    else:
        role = "attacker"

    col = i % 3
    row = i // 3
    x = rink_min_lon + col * (rink_max_lon - rink_min_lon) / 2
    y = rink_min_lat + row * (rink_max_lat - rink_min_lat)

    players[f"player_{i+1}"] = {
        "lat": BASE_LAT + random.uniform(-0.0004, 0.0004),
        "lon": BASE_LON + random.uniform(-0.0004, 0.0004),
        "direction": random.uniform(0, 2 * math.pi),
        "speed": 0.00003 + random.uniform(-0.00001, 0.00001),
        "target_lat": y,
        "target_lon": x,
        "role": role,
    }

# Initialize puck
puck = {
    "lat": BASE_LAT,
    "lon": BASE_LON,
    "speed": 0.0,
    "direction": 0.0,
}

def move_player(player_id):
    player = players[player_id]
    role = player["role"]

    # Vector toward puck
    dist_to_puck_x = puck["lon"] - player["lon"]
    dist_to_puck_y = puck["lat"] - player["lat"]
    dist_to_puck = math.sqrt(dist_to_puck_x ** 2 + dist_to_puck_y ** 2)

    # Strategy varies by role
    chase_puck = False
    if role == "attacker":
        chase_puck = True
    elif role == "midfielder":
        chase_puck = puck["speed"] > 0.0 and dist_to_puck < meters_to_lat(15.0)
    elif role == "defender":
        chase_puck = puck["speed"] > 0.0 and dist_to_puck < meters_to_lat(10.0)

    if chase_puck:
        to_puck_x = dist_to_puck_x / dist_to_puck
        to_puck_y = dist_to_puck_y / dist_to_puck
    else:
        # Patrol or hold zone
        dist_to_target_x = player["target_lon"] - player["lon"]
        dist_to_target_y = player["target_lat"] - player["lat"]
        dist_to_target = math.sqrt(dist_to_target_x ** 2 + dist_to_target_y ** 2)

        if dist_to_target > meters_to_lat(1.0):
            to_puck_x = dist_to_target_x / dist_to_target
            to_puck_y = dist_to_target_y / dist_to_target
        else:
            # Idle wander
            wander_angle = player["direction"] + random.uniform(-0.3, 0.3)
            to_puck_x = math.cos(wander_angle)
            to_puck_y = math.sin(wander_angle)

    # Avoidance from nearby players
    avoidance_x = 0.0
    avoidance_y = 0.0
    repulsion_radius = meters_to_lat(1.5)
    repulsion_strength = 0.0005

    for other_id, other in players.items():
        if other_id == player_id:
            continue
        dx = player["lon"] - other["lon"]
        dy = player["lat"] - other["lat"]
        dist_sq = dx * dx + dy * dy
        if 0 < dist_sq < repulsion_radius * repulsion_radius:
            dist = math.sqrt(dist_sq)
            avoidance_x += dx / dist * (repulsion_strength / dist)
            avoidance_y += dy / dist * (repulsion_strength / dist)

    move_x = to_puck_x + avoidance_x
    move_y = to_puck_y + avoidance_y

    length = math.sqrt(move_x * move_x + move_y * move_y)
    if length < 1e-5:
        move_x, move_y = to_puck_x, to_puck_y
        length = 1.0

    move_x /= length
    move_y /= length

    # Smooth direction change (lerp)
    new_dir = math.atan2(move_y, move_x)
    dir_diff = (new_dir - player["direction"] + math.pi) % (2 * math.pi) - math.pi
    max_turn = 0.3
    dir_diff = max(-max_turn, min(max_turn, dir_diff))
    player["direction"] += dir_diff

    # Move forward
    player["lat"] += math.sin(player["direction"]) * player["speed"]
    player["lon"] += math.cos(player["direction"]) * player["speed"]

    # Bounce off walls
    bounced = False
    if player["lat"] < rink_min_lat:
        player["lat"] = rink_min_lat
        bounced = True
    elif player["lat"] > rink_max_lat:
        player["lat"] = rink_max_lat
        bounced = True

    if player["lon"] < rink_min_lon:
        player["lon"] = rink_min_lon
        bounced = True
    elif player["lon"] > rink_max_lon:
        player["lon"] = rink_max_lon
        bounced = True

    if bounced:
        player["direction"] += math.pi + random.uniform(-0.3, 0.3)

def update_puck_position(player_id):
    player = players[player_id]
    dx = player["lon"] - puck["lon"]
    dy = player["lat"] - puck["lat"]
    dist_sq = dx * dx + dy * dy
    collision_radius = meters_to_lat(1.0)

    if dist_sq < collision_radius * collision_radius:
        dir_x = dx
        dir_y = dy
        length = math.sqrt(dir_x * dir_x + dir_y * dir_y)
        if length < 1e-5:
            return
        dir_x /= length
        dir_y /= length

        hit_speed = 0.0002
        puck["lat"] += dir_y * hit_speed
        puck["lon"] += dir_x * hit_speed
        puck["speed"] = hit_speed
        puck["direction"] = math.atan2(dir_y, dir_x)
    else:
        puck["speed"] *= 0.9
        if puck["speed"] < 1e-6:
            puck["speed"] = 0.0

    # Bounce off walls
    bounced = False
    if puck["lat"] < rink_min_lat or puck["lat"] > rink_max_lat:
        puck["direction"] = -puck["direction"]
        bounced = True

    if puck["lon"] < rink_min_lon or puck["lon"] > rink_max_lon:
        puck["direction"] = math.pi - puck["direction"]
        bounced = True

    if bounced and puck["speed"] > 0:
        dx = math.cos(puck["direction"]) * puck["speed"]
        dy = math.sin(puck["direction"]) * puck["speed"]
        puck["lat"] += dy
        puck["lon"] += dx

def generate_player_payload(player_id):
    player = players[player_id]

    # Realistic HR based on speed
    base_hr = 65
    exertion = min(player["speed"] / 0.00004, 1.0)
    hr_mean = base_hr + exertion * 100
    hr_value = int(random.gauss(hr_mean, 5))
    hr_value = max(50, min(190, hr_value))

    ecg_length = 100
    ecg_waveform = []

    for i in range(ecg_length):
        x = i / ecg_length
        y = 512

        if 0.1 < x < 0.2:
            y += 80 * math.sin((x - 0.1) * math.pi * 10)
        elif 0.25 < x < 0.27:
            y -= 150
        elif 0.27 <= x < 0.29:
            y += 300
        elif 0.29 <= x < 0.31:
            y -= 100
        elif 0.35 < x < 0.45:
            y += 100 * math.sin((x - 0.35) * math.pi * 10)
        else:
            y += random.gauss(0, 5)

        y = max(0, min(1023, y))
        ecg_waveform.append(int(y))

    return {
        "playerId": player_id,
        "latitude": player["lat"],
        "longitude": player["lon"],
        "hrValue": hr_value,
        "ecgSample": ecg_waveform,
        "timestamp": datetime.datetime.utcnow().isoformat() + "Z"
    }

async def handler(websocket):
    print("Client connected.")
    try:
        while True:
            await asyncio.sleep(1.0 / FPS)

            for player_id in players:
                move_player(player_id)

            for player_id in players:
                update_puck_position(player_id)
                if puck["speed"] > 0:
                    break

            puck["lat"] = clamp(puck["lat"], rink_min_lat, rink_max_lat)
            puck["lon"] = clamp(puck["lon"], rink_min_lon, rink_max_lon)

            message = {
                "players": [generate_player_payload(pid) for pid in players],
                "puck": {
                    "latitude": puck["lat"],
                    "longitude": puck["lon"],
                    "speed": puck["speed"]
                }
            }

            await websocket.send(json.dumps(message))
            print(f"[Server] Sent update with {len(players)} players + puck")
    except websockets.exceptions.ConnectionClosed:
        print("Client disconnected.")

async def main():
    print(f"Enhanced mock server running on ws://localhost:{PORT}")
    async with websockets.serve(handler, "localhost", PORT):
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())
