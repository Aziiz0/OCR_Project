using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Tesseract;

class Program
{
    static void Main(string[] args)
    {
        var pdfDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "PDFs");
        var outputImageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "OutputImages");
        var outputTextDirectory = Path.Combine(Directory.GetCurrentDirectory(), "OCR");

        // Check if the directories exist, if not, create them
        if (!Directory.Exists(pdfDirectoryPath)) Directory.CreateDirectory(pdfDirectoryPath);
        if (!Directory.Exists(outputImageDirectory)) Directory.CreateDirectory(outputImageDirectory);
        if (!Directory.Exists(outputTextDirectory)) Directory.CreateDirectory(outputTextDirectory);

        var pdfFiles = Directory.GetFiles(pdfDirectoryPath, "*.pdf");

        foreach (var pdfFile in pdfFiles)
        {
            Console.WriteLine($"Processing file {pdfFile}");

            var outputImagePath = Path.Combine(outputImageDirectory, Path.GetFileNameWithoutExtension(pdfFile));
            var command = $"pdftoppm -png \"{pdfFile}\" \"{outputImagePath}\"";
            Console.WriteLine($"Executing command: {command}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdftoppm", 
                    Arguments = $"-png \"{pdfFile}\" \"{outputImagePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // Redirect the standard error stream
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            // Read the error output
            string errorOutput = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                // If there's any error output, display it
                Console.WriteLine($"Error during conversion of {pdfFile}: {errorOutput}");
            }
            else
            {
                Console.WriteLine($"Conversion of {pdfFile} completed successfully");
            }

            // OCR each page
            var outputImages = Directory.GetFiles(outputImageDirectory, "*.png");

            foreach (var outputImage in outputImages)
            {
                try
                {
                    using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                    {
                        using (var img = Pix.LoadFromFile(outputImage))
                        {
                            using (var page = engine.Process(img))
                            {
                                var text = page.GetText();

                                // Write the OCR result to a text file in the OCR directory
                                var textFileName = Path.Combine(outputTextDirectory, Path.GetFileNameWithoutExtension(outputImage) + ".txt");
                                File.WriteAllText(textFileName, text);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Unexpected Error: " + e.Message);
                    Console.WriteLine("Details: ");
                    Console.WriteLine(e.ToString());
                }
            }
        }
    }
}
