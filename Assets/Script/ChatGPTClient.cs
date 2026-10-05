using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ChatGPTClient : MonoBehaviour
{
    private string apiKey;

    // OpenAI API
    private const string endpoint = "https://api.openai.com/v1/chat/completions";

    // 使用するモデル
    private const string model = "gpt-5.6-luna";

    // 実験ログに記録するため（README 6.3）
    public static string ModelName => model;

    // これ以上待っても返答が無ければ打ち切る（README 4.6）
    private const int TimeoutSeconds = 20;

    // 問い合わせの結果。失敗時はErrorに理由（timeout / network / http_<code> / parse / no_key）が入る
    public struct ChatResult
    {
        public bool Success;
        public string Text;
        public string Error;

        public static ChatResult Ok(string text) => new ChatResult { Success = true, Text = text };
        public static ChatResult Fail(string error) => new ChatResult { Success = false, Error = error };
    }

    [System.Serializable]
    public class ChatRequest
    {
        public string model;
        public Message[] messages;

        [System.Serializable]
        public class Message
        {
            public string role;
            public string content;
        }
    }

    [System.Serializable]
    public class ChatResponse
    {
        public Choice[] choices;

        [System.Serializable]
        public class Choice
        {
            public Message message;

            [System.Serializable]
            public class Message
            {
                public string role;
                public string content;
            }
        }
    }

    private void Awake()
    {
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("openai_key");

        if (keyFile == null)
        {
            Debug.LogError("API Key not found in Resources!");
            return;
        }

        apiKey = keyFile.text.Trim();
    }

    public async Task<ChatResult> SendChatMessage(
        string userText,
        string systemPrompt)
    {
        if (string.IsNullOrEmpty(apiKey))
            return ChatResult.Fail("no_key");

        string json = BuildRequest(userText, systemPrompt);

        using var request = new UnityWebRequest(endpoint, "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = TimeoutSeconds;

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "Authorization",
            "Bearer " + apiKey
        );

        var operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"OpenAI API Error: {request.responseCode}\n" +
                $"{request.error}\n" +
                $"{request.downloadHandler.text}"
            );

            string reason =
                request.result == UnityWebRequest.Result.ProtocolError ? "http_" + request.responseCode :
                request.error != null && request.error.ToLower().Contains("timeout") ? "timeout" :
                "network";

            return ChatResult.Fail(reason);
        }

        return ParseResponse(
            request.downloadHandler.text
        );
    }

    private string BuildRequest(
        string userText,
        string systemPrompt)
    {
        ChatRequest request = new ChatRequest
        {
            model = model,

            messages = new ChatRequest.Message[]
            {
                new ChatRequest.Message
                {
                    role = "system",
                    content = systemPrompt
                },

                new ChatRequest.Message
                {
                    role = "user",
                    content = userText
                }
            }
        };

        return JsonUtility.ToJson(request);
    }

    private ChatResult ParseResponse(string json)
    {
        try
        {
            ChatResponse response =
                JsonUtility.FromJson<ChatResponse>(json);

            if (response == null ||
                response.choices == null ||
                response.choices.Length == 0 ||
                response.choices[0].message == null)
            {
                Debug.LogError(
                    "Invalid response from OpenAI API."
                );

                return ChatResult.Fail("parse");
            }

            return ChatResult.Ok(response.choices[0].message.content);
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                $"Failed to parse OpenAI response: {e}"
            );

            return ChatResult.Fail("parse");
        }
    }
}
