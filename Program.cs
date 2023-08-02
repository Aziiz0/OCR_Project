using BitMiracle.Docotic.Pdf;
using Tesseract;
using Newtonsoft.Json;
using StringFiltering;
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

    static void ProcessPdfFiles(string pdfFile)
    {
        string logFilePath = "log.txt";

        // Load the log file
        List<string> log = File.Exists(logFilePath) ? File.ReadAllLines(logFilePath).ToList() : new List<string>();

        // If this PDF has already been processed, skip it
        if (log.Contains(pdfFile))
        {
            Console.WriteLine($"Skipping file {pdfFile} because it has already been processed.");
            return;
        }

        // Load the PDF document
        using (var pdf = new PdfDocument(pdfFile))
        {
            // Initialize Tesseract OCR engine
            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                string ocrResultsFile = $"ocr_results_{Path.GetFileNameWithoutExtension(pdfFile)}.txt";

                // Create a new instance of StringFilter
                var filter = new StringFilter();

                // Loop through each page in the PDF
                for (int i = 0; i < pdf.PageCount; ++i)
                {
                    PdfPage page = pdf.Pages[i];
                    string searchableText = page.GetText();

                    // If the page contains searchable text, skip this page
                    if (!string.IsNullOrEmpty(searchableText.Trim()))
                    {
                        continue;
                    }

                    // The page does not contain searchable text.
                    // Save it as a high-resolution image
                    PdfDrawOptions options = PdfDrawOptions.Create();
                    options.BackgroundColor = new PdfRgbColor(255, 255, 255);
                    options.HorizontalResolution = 300;
                    options.VerticalResolution = 300;

                    string pageImage = $"page_{i}.png";
                    page.Save(pageImage, options);

                    // Extract the contours from the image
                    var contourImages = ImageProcessor.ExtractContours(pageImage, i);

                    // Perform OCR on each contour image and delete it afterwards
                    foreach(var contourImage in contourImages)
                    {
                        // Perform OCR on the contour image
                        string ocrText = PageOCR(engine, contourImage);

                        // Reduce the whitespace in the OCR text
                        ocrText = filter.ReduceWhitespace(ocrText);

                        // Append the OCR result to the text file only if it's not empty or a newline
                        if (!string.IsNullOrEmpty(ocrText) && ocrText != "\n")
                        {
                            using (StreamWriter writer = new StreamWriter(ocrResultsFile, append: true))
                            {
                                writer.WriteLine(ocrText);
                            }
                        }

                        // Delete the contour image
                        //File.Delete(contourImage);
                    }

                    // Delete the original image of the page
                    //File.Delete(pageImage);
                }
            }
            log.Add(pdfFile);
            File.WriteAllLines(logFilePath, log);
        }
    }

    static string PageOCR(TesseractEngine engine, string contourImage)
    {
        // Load the image
        using (Pix img = Pix.LoadFromFile(contourImage))
        {
            // Perform OCR on the image
            using (Page recognizedPage = engine.Process(img))
            {
                // Return the recognized text
                return recognizedPage.GetText();
            }
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

        // Define the prompt for json output
        //string prompt = $"Given the following text, extract the patient's name, total charge, and total miles and return the result in JSON format: \n\n{text}\n\nExample output: \n\n{{\"PatientName\": \"John Doe\", \"TotalMiles\": \"2\", \"TotalCharge\": \"$200.00\"}}";

        // Define the prompt for json output with type information
        string prompt = $"Given the following text, extract the patient's name, total charge, and total miles and return the result in JSON format where each field is an object with 'type' and 'value' properties: \n\n{text}\n\nExample output: \n\n{{\"PatientName\": {{\"type\": \"string\", \"value\": \"John Doe\"}}, \"TotalMiles\": {{\"type\": \"decimal\", \"value\": \"2\"}}, \"TotalCharge\": {{\"type\": \"decimal\", \"value\": \"200.00\"}}}}";

        // Make the API call
        var result = await proxy.Ask(prompt);

        // Return the result
        return result;
    }

    static Dictionary<string, string> ExtractInfo(string filePath)
    {
        // Read the text from the file
        string text = File.ReadAllText(filePath);

        // Define regular expressions for each piece of information
        string namePattern = @"PATIENT’S NAME \(Last Name, First Name, Middle Initial\) ([A-Z, ]+)";
        string chargePattern = @"TOTAL CHARGE \$ ([0-9,.]+)";
        string milesPattern = @"TOTAL MILES:.* ([0-9\.]+)";

        // Use the regular expressions to find the information
        Match nameMatch = Regex.Match(text, namePattern);
        Match chargeMatch = Regex.Match(text, chargePattern);
        Match milesMatch = Regex.Match(text, milesPattern);

        // Extract the matched groups and return them in a dictionary
        return new Dictionary<string, string>
        {
            {"PatientName", nameMatch.Success ? nameMatch.Groups[1].Value : "Not found"},
            {"TotalCharge", chargeMatch.Success ? chargeMatch.Groups[1].Value : "Not found"},
            {"TotalMiles", milesMatch.Success ? milesMatch.Groups[1].Value : "Not found"}
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

        // Loop through each PDF file and process it
        foreach (var pdfFile in pdfFiles)
        {
            Console.WriteLine($"Processing file {pdfFile}");
            ProcessPdfFiles(pdfFile);
            
            string ocrResultsFile = $"ocr_results_{Path.GetFileNameWithoutExtension(pdfFile)}.txt";

            /*// Now that we have all the text from the PDF, we can analyze it with AI
            string aiAnalysisResult = AnalyzeFileWithAI(ocrResultsFile).Result;

            // Write the AI analysis result to a text file
            using (StreamWriter writer = new StreamWriter("ai_analysis_result.txt"))
            {
                writer.Write(aiAnalysisResult);
            }*/
            
            //User user = CreateUserRecord(aiAnalysisResult);
            //Dictionary<string, object> user = CreateDynamicRecord(aiAnalysisResult);
            Dictionary<string, string> user = ExtractInfo(ocrResultsFile);

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
                Console.WriteLine($"Total miles: {user["TotalMiles"]}");
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine("TotalMiles not found in the data.");
            }

        }
    }
}
