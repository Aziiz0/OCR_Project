using BitMiracle.Docotic.Pdf;
using Tesseract;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        // Define the path for the PDF files to be processed
        var pdfDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "PDFs");
        var outputTextDirectory = Path.Combine(Directory.GetCurrentDirectory(), "OutputTexts");

        // Check if the directories exist, if not, create them
        if (!Directory.Exists(pdfDirectoryPath)) Directory.CreateDirectory(pdfDirectoryPath);
        if (!Directory.Exists(outputTextDirectory)) Directory.CreateDirectory(outputTextDirectory);

        // Get all the PDF files in the directory
        var pdfFiles = Directory.GetFiles(pdfDirectoryPath, "*.pdf");

        // Loop through each PDF file and process it
        foreach (var pdfFile in pdfFiles)
        {
            Console.WriteLine($"Processing file {pdfFile}");
            ProcessPdfFiles(pdfFile);
        }
    }

    static void ProcessPdfFiles(string pdfFile)
    {
        // Load the PDF document
        using (var pdf = new PdfDocument(pdfFile))
        {
            // Initialize Tesseract OCR engine
            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
            {
                StringBuilder allText = new StringBuilder();

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
                        // Perform OCR on the contour image and append the result to the allText StringBuilder
                        allText.AppendLine(PageOCR(engine, contourImage));

                        // Delete the contour image
                        File.Delete(contourImage);
                    }

                    // Delete the original image of the page
                    File.Delete(pageImage);
                }

                // Now that we have all the text from the PDF, we can analyze it with AI
                string aiAnalysisResult = AnalyzeTextWithAI(allText.ToString()).Result;

                // Write the AI analysis result to a text file
                using (StreamWriter writer = new StreamWriter("ai_analysis_result.txt"))
                {
                    writer.Write(aiAnalysisResult);
                }
            }
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

    public static Dictionary<string, string> CreatePatientRecord(string patientName, string totalCharge)
    {
        var record = new Dictionary<string, string>
        {
            { "PatientName", patientName },
            { "TotalCharge", totalCharge }
        };

        return record;
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

        // Define the prompt
        string prompt = $"Extract the patient's name and total charge from the following text: {text}";

        // Make the API call
        var result = await proxy.Ask(prompt);

        // Return the result
        return result;
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
}
