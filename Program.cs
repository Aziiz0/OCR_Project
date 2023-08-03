using Tesseract;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

class Program
{
    public class User
    {
        public string PatientName { get; set; }
        public decimal TotalCharge { get; set; }
        public decimal TotalMiles { get; set; } // change to match the AI output
        // Add other properties as needed
    }

    public static User CreateUserRecord(string jsonString)
    {
        try
        {
            var record = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

            if (record == null)
            {
                return new User();
            }

            User user = new User();
            user.PatientName = record.ContainsKey("PatientName") ? record["PatientName"] : null;

            if (record.ContainsKey("TotalCharge"))
            {
                // Convert the TotalCharge value to a decimal
                decimal? parsedCharge = ConvertToDecimal(record["TotalCharge"]);
                if (parsedCharge.HasValue)
                {
                    // If parsing was successful, assign the parsed value to the TotalCharge property
                    user.TotalCharge = parsedCharge.Value;
                }
                else
                {
                    // If parsing was unsuccessful, set TotalCharge to 0 or any default value
                    user.TotalCharge = 0;
                }
            }

            if (record.ContainsKey("TotalMiles"))
            {
                // Convert the TotalMiles value to a decimal
                decimal? parsedMiles = ConvertToDecimal(record["TotalMiles"]);
                if (parsedMiles.HasValue)
                {
                    // If parsing was successful, assign the parsed value to the TotalMiles property
                    user.TotalMiles = parsedMiles.Value;
                }
                else
                {
                    // If parsing was unsuccessful, set TotalMiles to 0 or any default value
                    user.TotalMiles = 0;
                }
            }

            return user;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Could not convert string to User: {ex.Message}");
            return new User();
        }
    }

    public static Dictionary<string, object> CreateDynamicRecord(string jsonString)
    {
        try
        {
            var record = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(jsonString);

            if (record == null)
            {
                return new Dictionary<string, object>();
            }

            var dynamicRecord = new Dictionary<string, object>();

            foreach (var entry in record)
            {
                if (entry.Value["type"] == "string")
                {
                    dynamicRecord[entry.Key] = entry.Value["value"];
                }
                else if (entry.Value["type"] == "decimal")
                {
                    decimal? parsedValue = ConvertToDecimal(entry.Value["value"]);
                    dynamicRecord[entry.Key] = parsedValue.HasValue ? parsedValue.Value : 0;
                }
                // handle more types as needed...
            }

            return dynamicRecord;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Could not convert string to record: {ex.Message}");
            return new Dictionary<string, object>();
        }
    }

    public static decimal? ConvertToDecimal(string value)
    {
        // Remove the dollar sign and comma, then parse the value as a decimal
        string numericString = value.Replace("$", "").Replace(",", "");
        if (decimal.TryParse(numericString, out decimal parsedValue))
        {
            // If parsing was successful, return the decimal value
            return parsedValue;
        }
        else
        {
            // If parsing was unsuccessful, return null
            return null;
        }
    }

    static Dictionary<string, object> ExtractInfo(string filePath)
    {
        // Read the text from the file
        string text = File.ReadAllText(filePath);

        // Define regular expressions for each piece of information
        string namePattern = @"PATIENT\W*S NAME \(Last Name, First Name, Middle Initial\) ([A-Z, ]+)";
        string chargePattern = @"TOTAL CHARGE \$ ([0-9,.]+)";
        List<string> diagnoses = new List<string>();
        string[] parts = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };

        foreach (string part in parts)
        {
            string diagnosisPattern = $@"{part}\.? (?:{part}(?:L)? )?([A-Z0-9]{{3}}\.[A-Z0-9]{{3,4}})";
            Match m = Regex.Match(text, diagnosisPattern);
            if (m.Success)
            {
                diagnoses.Add(m.Groups[1].Value);
            }
            else
            {
                break;
            }
        }

        // Use the regular expressions to find the information
        Match nameMatch = Regex.Match(text, namePattern);
        Match chargeMatch = Regex.Match(text, chargePattern);

        // Extract the matched groups and return them in a dictionary
        return new Dictionary<string, object>
        {
            {"PatientName", nameMatch.Success ? nameMatch.Groups[1].Value : "Not found"},
            {"TotalCharge", chargeMatch.Success ? chargeMatch.Groups[1].Value : "Not found"},
            {"Diagnosis", diagnoses.Count > 0 ? diagnoses : new List<string> { "Not found" }}
        };
    }

    static void TestPageSegModes(TesseractEngine engine, string pageImage, int pageNumber)
    {
        // Load the image
        using (Pix img = Pix.LoadFromFile(pageImage))
        {
            // Define the output file path.
            string outputFilePath = $"test_output_page_{pageNumber}.txt";

            // Create a new StreamWriter.
            using (StreamWriter writer = new StreamWriter(outputFilePath, append: true))
            {
                // Iterate over all the PageSegMode values
                foreach (PageSegMode mode in Enum.GetValues(typeof(PageSegMode)))
                {
                    if (mode == PageSegMode.OsdOnly)
                    {
                        continue;  // Skip OsdOnly mode
                    }

                    // Perform OCR using the current PageSegMode
                    using (Page recognizedPage = engine.Process(img, mode))
                    {
                        // Write the OCR results to the output file
                        writer.WriteLine($"Testing mode {mode} on page {pageNumber}");
                        writer.WriteLine($"Mean confidence: {recognizedPage.GetMeanConfidence()}");
                        writer.WriteLine($"Text: {recognizedPage.GetText()}");
                        writer.WriteLine();
                    }
                }
            }
        }
    }

    static void Main(string[] args)
    {
        // Define the path for the PDF files to be processed
        var pdfDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "PDFs");

        // Check if the directories exist, if not, create them
        if (!Directory.Exists(pdfDirectoryPath)) Directory.CreateDirectory(pdfDirectoryPath);

        // Get all the PDF files in the directory
        var pdfFiles = Directory.GetFiles(pdfDirectoryPath, "*.pdf");

        // Create an instance of PDFProcessor
        var processor = new PDFProcessor(@"./tessdata", @"C:\Users\adeeb\OneDrive\Documents\GitHub\PDFToImage\dist\pdf_to_images.exe");

        // Specify the pages you want to process
        List<int> pagesToProcess = new List<int> { 1 };

        // Loop through each PDF file and process it
        foreach (var pdfFile in pdfFiles)
        {
            Console.WriteLine($"Processing file {pdfFile}");
            processor.ProcessPdf(pdfFile, pagesToProcess);

            string ocrResultsFile = $"ocr_results_{Path.GetFileNameWithoutExtension(pdfFile)}.txt";


            /*// Now that we have all the text from the PDF, we can analyze it with AI
            string aiAnalysisResult = await AIChat.AnalyzeFileWithAI(ocrResultsFile).Result;

            // Write the AI analysis result to a text file
            using (StreamWriter writer = new StreamWriter("ai_analysis_result.txt"))
            {
                writer.Write(aiAnalysisResult);
            }*/
            
            //User user = CreateUserRecord(aiAnalysisResult);
            //Dictionary<string, object> user = CreateDynamicRecord(aiAnalysisResult);
            Dictionary<string, object> user = ExtractInfo(ocrResultsFile);

            try
            {
                Console.WriteLine($"Patient name: {user["PatientName"]}");
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine("PatientName not found in the data.");
            }

            try
            {
                Console.WriteLine($"Total charge: {user["TotalCharge"]}");
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine("TotalCharge not found in the data.");
            }

            try
            {
                List<string> diagnoses = user["Diagnosis"] as List<string>;
                Console.WriteLine("Diagnoses:");
                foreach (string diagnosis in diagnoses)
                {
                    Console.WriteLine(diagnosis);
                }
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine("Diagnosis not found in the data.");
            }
        }
    }
}
