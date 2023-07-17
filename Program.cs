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

            var documentText = new StringBuilder();
            using (var pdf = new PdfDocument(pdfFile))
            {
                using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                {
                    for (int i = 0; i < pdf.PageCount; ++i)
                    {
                        if (documentText.Length > 0)
                            documentText.Append("\r\n\r\n");

                        PdfPage page = pdf.Pages[i];
                        string searchableText = page.GetText();

                        // Simple check if the page contains searchable text.
                        // We do not need to perform OCR in that case.
                        if (!string.IsNullOrEmpty(searchableText.Trim()))
                        {
                            documentText.Append(searchableText);
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

                        // Perform OCR
                        using (Pix img = Pix.LoadFromFile(pageImage))
                        {
                            using (Page recognizedPage = engine.Process(img))
                            {
                                Console.WriteLine($"Mean confidence for page #{i}: {recognizedPage.GetMeanConfidence()}");

                                string recognizedText = recognizedPage.GetText();
                                documentText.Append(recognizedText);
                            }
                        }
                        
                        File.Delete(pageImage);
                    }
                }
            }

            // Write the OCR result to a text file in the OCR directory
            var textFileName = Path.Combine(outputTextDirectory, Path.GetFileNameWithoutExtension(pdfFile) + ".txt");
            File.WriteAllText(textFileName, documentText.ToString());

            Console.WriteLine($"Text extraction from {pdfFile} completed successfully");
        }
    }
}
