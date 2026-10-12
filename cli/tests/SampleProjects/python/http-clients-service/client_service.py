import requests
import httpx
import aiohttp
from flask import Flask, jsonify

app = Flask(__name__)

@app.get("/sync-fetch")
def sync_fetch():
    resp1 = requests.get("https://api.github.com/users")
    resp2 = requests.post("https://httpbin.org/post", json={"key": "val"})

    with requests.Session() as s:
        resp3 = s.get("https://api.example.com/session-data")

    return jsonify({"github": resp1.status_code, "httpbin": resp2.status_code, "session": resp3.status_code})

@app.get("/httpx-fetch")
def httpx_fetch():
    resp1 = httpx.get("https://api.stripe.com/v1/charges")

    with httpx.Client() as client:
        resp2 = client.get("https://api.stripe.com/v1/customers")

    return jsonify({"charges": resp1.status_code, "customers": resp2.status_code})

async def async_httpx_fetch():
    async with httpx.AsyncClient() as client:
        resp = await client.post("https://api.stripe.com/v1/refunds")
        return resp.status_code

async def aiohttp_fetch():
    async with aiohttp.ClientSession() as session:
        async with session.get("https://api.weather.gov/alerts") as resp:
            return resp.status

    resp2 = await aiohttp.ClientSession().post("https://api.weather.gov/stations")
    return resp2.status
