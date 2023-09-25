using System.Diagnostics;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Tesseract;
using StringFiltering;
using PdfiumViewer;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;

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
        using (var engine = new TesseractEngine(tessdataDir, "eng", EngineMode.TesseractAndLstm))
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
                using var src = new Image<Bgr, byte>(pageImage);
                var minDimension = new Size((int)(src.Width / 30), (int)(src.Height / 35));  // set min width and height
                var maxDimension = new Size((int)(src.Width / 1.4), (int)(src.Height / 3));  // set max width and height

                // Extract the contours from the image
                // var contourImages = ImageProcessor.ExtractContours(pageImage, 1, 11, 2, minDimension, maxDimension);
                Size optimizedMinDimension = new Size(91, 104);
                Size optimizedMaxDimension = new Size(3645, 2349);
                int optimizedBlockSize = 13;
                int optimizedCValue = -2;
                // Size optimizedMinDimension = new Size(243, 234);
                // Size optimizedMaxDimension = new Size(7290, 9396);
                // int optimizedBlockSize = 3;
                // int optimizedCValue = 2;
                // Size optimizedMinDimension = new Size(91, 117);
                // Size optimizedMaxDimension = new Size(3645, 2349);
                // int optimizedBlockSize = 13;
                // int optimizedCValue = -4;

                var contourData = ImageProcessor.ExtractContours(pageImage, 1, optimizedBlockSize, optimizedCValue, optimizedMinDimension, optimizedMaxDimension);

                // Perform OCR on each contour image and delete it afterwards
                foreach (var data in contourData)
                {
                    string contourImage = data.Item1; // This is the path of the image

                    // Perform OCR on the contour image
                    string ocrText = PageOCR(engine, contourImage, 0.85f);

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

            // Call the FixBoxAboveText function to correct the image
            ImageProcessor.FixBoxAboveText(outputImage, "E.", "F.");
        }
    }

    public static string PageOCR(TesseractEngine engine, string imageFile, float confidenceThreshold = 0.75f)
    {
        StringBuilder filteredText = new StringBuilder();

        // 1. Detect checkboxes
        // var checkedBoxes = ImageProcessor.DetectCheckBoxes(imageFile, 0.75);

        // OCR Processing
        using (var img = Pix.LoadFromFile(imageFile))
        {
            using (var page = engine.Process(img))
            {
                using (var iter = page.GetIterator())
                {
                    iter.Begin();
                    do
                    {
                        float confidence = iter.GetConfidence(PageIteratorLevel.Word);
                        if (confidence >= confidenceThreshold)
                        {
                            var word = iter.GetText(PageIteratorLevel.Word);
                            filteredText.Append(word + " ");
                        }
                    } while (iter.Next(PageIteratorLevel.Word));
                }
            }
        }

        // 3. Append the detected checkbox position to the result string
        // foreach (var box in checkedBoxes)
        // {
        //     filteredText.Append($"BOX: {box} ");
        // }

        return filteredText.ToString().Trim();
    }

    public static void AddFieldToDictionaryFromPattern(Dictionary<string, object> dataDict, string text, string pattern, string fieldName)
    {
        Match match = Regex.Match(text, pattern, RegexOptions.Multiline);
        
        // Debug prints to understand the match
        // Console.WriteLine($"Trying to match pattern: {pattern}");
        if (match.Success)
        {
            // Console.WriteLine("Matched!");

            // for (int i = 0; i < match.Groups.Count; i++)
            // {
            //     Console.WriteLine($"Group {i}: {match.Groups[i].Value}");
            // }

            dataDict[fieldName] = string.Concat(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        }
        else
        {
            Console.WriteLine("No match found!");
            dataDict[fieldName] = "Not found";
        }
    }

    public static bool AddListFieldToDictionaryFromPattern(Dictionary<string, object> dataDict, string text, string pattern, string fieldName)
    {
        MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.Multiline);
        List<string> results = new List<string>();

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                results.Add(match.Groups[1].Value);
            }
        }

        if (results.Count == 0)
        {
            results.Add("Not found");
            return false;
        }

        dataDict[fieldName] = results;
        return true;
    }

    public static void CleanDictionaryValues(Dictionary<string, object> dataDict)
    {
        var keys = dataDict.Keys.ToList();
        foreach (var key in keys)
        {
            if (dataDict[key] is string stringValue)
            {
                dataDict[key] = Regex.Replace(stringValue, @"\s+,", ",");
            }
        }
    }


}