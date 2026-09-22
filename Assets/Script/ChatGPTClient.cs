using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ChatGPTClient : MonoBehaviour
{
    private string apiKey;

    [SerializeField] private PromptData promptData;

    // OpenAI API
    private const string endpoint = "https://api.openai.com/v1/chat/completions";

    // 使用するモデル
    private const string model = "gpt-5.6-luna";

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

    public async Task<string> SendChatMessage(
        string userText,
        string systemPrompt)
    {
        string json = BuildRequest(userText, systemPrompt);

        using var request = new UnityWebRequest(endpoint, "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

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

            return "エラー";
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

    private string ParseResponse(string json)
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

                return "エラー";
            }

            return response.choices[0].message.content;
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                $"Failed to parse OpenAI response: {e}"
            );

            return "エラー";
        }
    }
}