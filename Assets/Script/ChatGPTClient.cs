using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ChatGPTClient : MonoBehaviour
{
    private string apiKey;
    [SerializeField] private PromptData promptData;

    private const string endpoint = "https://api.openai.com/v1/chat/completions";

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

    public async Task<string> SendChatMessage(string userText, string systemPrompt)
    {
        var json = BuildRequest(userText, systemPrompt);

        using var request = new UnityWebRequest(endpoint, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        var operation = request.SendWebRequest();

        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
            return "エラー";
        }

        return ParseResponse(request.downloadHandler.text);
    }

    private string ParseResponse(string json)
    {
        int start = json.IndexOf("content") + 11;
        int end = json.IndexOf("\"", start);
        return json.Substring(start, end - start);
    }

    private string BuildRequest(string userText, string systemPrompt)
    {
        ChatRequest req = new ChatRequest
        {
            model = "gpt-4o-mini",
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

        return JsonUtility.ToJson(req);
    }
}