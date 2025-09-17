using UnityEngine;

public class TestAPI : MonoBehaviour
{
    public APIManager apiManager; // drag your APIManager here in Inspector

    void Start()
    {
        if (apiManager != null)
        {
            StartCoroutine(apiManager.FetchAllPlayerData());
        }
        else
        {
            Debug.LogError("APIManager reference not set in Inspector!");
        }
    }
}
