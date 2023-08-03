using System.Diagnostics;
using Tesseract;
using StringFiltering;
using PdfiumViewer;

public class PDFProcessor
{
    private string tessdataDir;
    private string pythonScriptPath;

    public PDFProcessor(string tessdataDir, string pythonScriptPath)
    {
        this.tessdataDir = tessdataDir;
        this.pythonScriptPath = pythonScriptPath;
    }

    public void ProcessPdf(string pdfFile, IEnumerable<int> pagesToProcess)
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

        string outputBase = $"output_{Path.GetFileNameWithoutExtension(pdfFile)}";

        // Initialize Tesseract OCR engine
        using (var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.Default))
        {
            string ocrResultsFile = $"ocr_results_{Path.GetFileNameWithoutExtension(pdfFile)}.txt";

            // Create a new instance of StringFilter
            var filter = new StringFilter();

            // Convert only the specific pages to images
            foreach (int page in pagesToProcess)
            {
                string pageImage = $"{outputBase}_{page}.png";

                // Call Python script to convert the specific PDF page to image
                ConvertPdfPageToImage(pdfFile, page, pageImage);

                // Extract the contours from the image
                var contourImages = ImageProcessor.ExtractContours(pageImage, page);

                // Perform OCR on each contour image and delete it afterwards
                foreach (var contourImage in contourImages)
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

    private void ConvertPdfPageToImage(string pdfFile, int pageNumber, string outputImage)
    {
        using (var stream = new FileStream(pdfFile, FileMode.Open, FileAccess.Read))
        using (var document = PdfDocument.Load(stream))
        {
            // Set the desired width in pixels
            int desiredWidth = 1500;

            // Get the size of the page in points (1 point = 1/72 inch)
            var pageSize = document.PageSizes[pageNumber - 1];

            // Calculate the necessary DPI to achieve the desired width
            float dpi = 72f * desiredWidth / pageSize.Width;

            // Calculate the height to maintain the same aspect ratio
            int height = (int)(pageSize.Height * dpi / 72f);

            Console.WriteLine($"Page: {pageNumber}, DPI: {dpi}, Size: ({desiredWidth}, {height})");

            // Render the page to an image with the desired DPI and size
            var image = document.Render(pageNumber - 1, desiredWidth, height, dpi, dpi, PdfRotation.Rotate0, PdfRenderFlags.CorrectFromDpi);

            image.Save(outputImage, System.Drawing.Imaging.ImageFormat.Png);
        }
    }




    private string PageOCR(TesseractEngine engine, string imageFile)
    {
        using (var img = Pix.LoadFromFile(imageFile))
        {
            using (var page = engine.Process(img))
            {
                return page.GetText();
            }
        }
    }
}