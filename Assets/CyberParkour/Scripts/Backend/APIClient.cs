using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ApiResult
{
    public bool NetworkError;
    public long Status;
    public string Body = "";
    public bool Success => !NetworkError && Status >= 200 && Status < 300;
}

/// <summary>
/// Thin async HTTPS client for Supabase (Auth + PostgREST RPC). Never blocks the main thread.
/// Network failures (no response) are retried with backoff; HTTP errors are not.
/// Retrying is safe because every state-changing call carries an idempotency key.
/// </summary>
public class APIClient
{
    readonly SupabaseConfig cfg;
    public APIClient(SupabaseConfig config) { cfg = config; }

    public async Task<ApiResult> PostJson(string path, string json, string bearer, int maxAttempts = 3)
    {
        ApiResult last = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            last = await SendOnce(path, json, bearer);
            if (!last.NetworkError) return last;
            if (attempt < maxAttempts) await Task.Delay(400 * attempt);
        }
        return last;
    }

    async Task<ApiResult> SendOnce(string path, string json, string bearer)
    {
        using (var req = new UnityWebRequest(cfg.BaseUrl + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(string.IsNullOrEmpty(json) ? "{}" : json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("apikey", cfg.Key);
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            else if (cfg.UsesJwtKey) req.SetRequestHeader("Authorization", "Bearer " + cfg.Key);
            req.timeout = 15;

            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            await tcs.Task;

            var res = new ApiResult();
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.DataProcessingError)
            {
                res.NetworkError = true;
                res.Body = req.error;
            }
            else
            {
                res.Status = req.responseCode;
                res.Body = req.downloadHandler != null ? req.downloadHandler.text : "";
            }
            return res;
        }
    }
}
