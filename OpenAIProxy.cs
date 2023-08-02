using ChatGPT.Net;

public class OpenAIProxy
{
    private readonly ChatGpt _bot;

    public OpenAIProxy(string apiKey)
    {
        _bot = new ChatGpt(apiKey);
    }

    public async Task<string> Ask(string prompt)
    {
        var response = await _bot.Ask(prompt);
        return response;
    }

    public async Task AskStream(Action<string> onResponse, string prompt)
    {
        await _bot.AskStream(onResponse, prompt);
    }

    public async Task<string> AskInConversation(string prompt, string conversationName)
    {
        var response = await _bot.Ask(prompt, conversationName);
        return response;
    }

    public async Task AskStreamInConversation(Action<string> onResponse, string prompt, string conversationName)
    {
        await _bot.AskStream(onResponse, prompt, conversationName);
    }
}
