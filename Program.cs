using System;
using System.IO;
using System.Text;
using BitMiracle.Docotic.Pdf;
using Tesseract;

class Program
{
    static void Main(string[] args)
    {
        var pdfDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "PDFs");
        var outputTextDirectory = Path.Combine(Directory.GetCurrentDirectory(), "OutputTexts");

        // Check if the directories exist, if not, create them
        if (!Directory.Exists(pdfDirectoryPath)) Directory.CreateDirectory(pdfDirectoryPath);
        if (!Directory.Exists(outputTextDirectory)) Directory.CreateDirectory(outputTextDirectory);

        var pdfFiles = Directory.GetFiles(pdfDirectoryPath, "*.pdf");

        foreach (var pdfFile in pdfFiles)
        {
            Console.WriteLine($"Processing file {pdfFile}");

            using (var pdf = new PdfDocument(pdfFile))
            {
                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                {
                    for (int i = 0; i < pdf.PageCount; ++i)
                    {
                        PdfPage page = pdf.Pages[i];
                        string searchableText = page.GetText();

                        // Simple check if the page contains searchable text.
                        // We do not need to perform OCR in that case.
                        if (!string.IsNullOrEmpty(searchableText.Trim()))
                        {
                            continue;
                        }

                        // This page is not searchable.
                        // Save the page as a high-resolution image
                        PdfDrawOptions options = PdfDrawOptions.Create();
                        options.BackgroundColor = new PdfRgbColor(255, 255, 255);
                        options.HorizontalResolution = 300;
                        options.VerticalResolution = 300;

                        string pageImage = $"page_{i}.png";
                        page.Save(pageImage, options);

                        // Perform OCR with different modes
                        TestPageSegModes(engine, pageImage, i);

                        File.Delete(pageImage);
                    }
                }
            }
        }
    }
    static void TestPageSegModes(TesseractEngine engine, string pageImage, int pageNumber)
    {
        using (Pix img = Pix.LoadFromFile(pageImage))
        {
            // Define the output file path. You can adjust this as needed.
            string outputFilePath = $"test_output_page_{pageNumber}.txt";

            // Create a new StreamWriter. This will automatically create the file if it doesn't exist.
            using (StreamWriter writer = new StreamWriter(outputFilePath))
            {
                foreach (PageSegMode mode in Enum.GetValues(typeof(PageSegMode)))
                {
                    if (mode == PageSegMode.OsdOnly)
                    {
                        continue;  // Skip OsdOnly mode
                    }

                    using (Page recognizedPage = engine.Process(img, mode))
                    {
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
