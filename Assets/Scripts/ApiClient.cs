using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class ApiClient
{
    public static void fetch(
        string url,
        Action<bool, string> callback,
        string method = "GET",
        Dictionary<string, string> queryParams = null,
        string jsonBody = null,
        MonoBehaviour runner = null
    )
    {
        if (runner == null)
        {
            runner = GameObject.FindObjectOfType<GameManager>();
            if (runner == null)
            {
                Debug.LogError("ApiClient.fetch requires a MonoBehaviour runner (like GameManager) to start coroutines.");
                return;
            }
        }

        runner.StartCoroutine(FetchRoutine(url, callback, method, queryParams, jsonBody));
    }

    private static IEnumerator FetchRoutine(
        string url,
        Action<bool, string> callback,
        string method,
        Dictionary<string, string> queryParams,
        string jsonBody
    )
    {
        // Append query params if provided
        if (queryParams != null && queryParams.Count > 0)
        {
            var sb = new StringBuilder("?");
            foreach (var kvp in queryParams)
            {
                sb.Append(UnityWebRequest.EscapeURL(kvp.Key))
                  .Append("=")
                  .Append(UnityWebRequest.EscapeURL(kvp.Value))
                  .Append("&");
            }
            sb.Length--; // remove last "&"
            url += sb.ToString();
        }

        UnityWebRequest request;

        if (method.ToUpper() == "POST")
        {
            byte[] bodyRaw = string.IsNullOrEmpty(jsonBody) ? null : Encoding.UTF8.GetBytes(jsonBody);
            request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
        }
        else
        {
            request = UnityWebRequest.Get(url);
        }

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            callback(false, request.error);
        }
        else
        {
            callback(true, request.downloadHandler.text);
        }
    }
}
