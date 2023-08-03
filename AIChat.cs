using System;
using System.IO;
using System.Threading.Tasks;

public class AIChat
{
    public static async Task<string> AnalyzeFileWithAI(string filePath)
    {
        string text = File.ReadAllText(filePath);
        return await AnalyzeTextWithAI(text);
    }

    public static async Task<string> AnalyzeTextWithAI(string text)
    {
        // Initialize the OpenAI API with your API key
        string apiKey = Environment.GetEnvironmentVariable("OpenAi_API_Key") ?? throw new Exception("OpenAI API key not found");

        // Create an instance of the OpenAIProxy class
        var proxy = new OpenAIProxy(apiKey);

        // Define the prompt for json output with type information
        string prompt = $"Given the following text, extract the patient's name, total charge, and 21. all diagnosis and return the result in JSON format where each field is an object with 'type' and 'value' properties: \n\n{text}\n\nExample output: \n\n{{\"PatientName\": {{\"type\": \"string\", \"value\": \"John Doe\"}}, \"Diagnosis\": {{\"type\": \"string\", \"value\": \"A00.00AA\"}}, \"TotalCharge\": {{\"type\": \"decimal\", \"value\": \"200.00\"}}}}";

        // Make the API call
        var result = await proxy.Ask(prompt);

        // Return the result
        return result;
    }
}
