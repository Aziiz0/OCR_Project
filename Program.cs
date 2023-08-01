using System;
using System.Diagnostics;
using System.IO;
using BitMiracle.Docotic.Pdf;
using Tesseract;

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

                    // Create a new StreamWriter.
                    string outputFilePath = $"output_page_{i}.txt";
                    using (StreamWriter writer = new StreamWriter(outputFilePath))
                    {
                        // Perform OCR on each contour image and delete it afterwards
                        foreach(var contourImage in contourImages)
                        {
                            // Perform OCR on the contour image and write the result to the output file
                            PageOCR(engine, contourImage, writer);

                            // Delete the contour image
                            //File.Delete(contourImage);
                        }
                    }

                    // Delete the original image of the page
                    File.Delete(pageImage);
                }
            }
        }
    }

    static void PageOCR(TesseractEngine engine, string contourImage, StreamWriter writer)
    {
        // Load the image
        using (Pix img = Pix.LoadFromFile(contourImage))
        {
            // Perform OCR on the image
            using (Page recognizedPage = engine.Process(img))
            {
                // Write the recognized text to the output file
                writer.Write(recognizedPage.GetText());
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
