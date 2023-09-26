using Tesseract;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Newtonsoft.Json;

class Program
{
    // static void Main(string[] args)
    // {
    //     ImageProcessor.DetectTopLeftCorners("output_WPH1H0BYBYD_1.png", 90);
    // }

    static void Main(string[] args)
    {
        // {   // OPTIMIZE CLEAN TEXT PARAMETERS

        //     List<string> imageFile = new List<string> {@"CroppedImages\contour_1_466.png", @"CroppedImages\contour_1_724.png", @"CroppedImages\contour_1_1770.png",
        //                                                @"CroppedImages\contour_1_3983.png", @"CroppedImages\contour_1_3986.png", @"CroppedImages\contour_1_3987.png"};
            
        //     var bestParams = ImageProcessor.OptimizeCleanTextParameters(@"./tessdata", "eng", imageFile);
        //     Console.WriteLine($"Best Parameters: Kernel Size = {bestParams.Item1}, Iterations = {bestParams.Item2}, Threshold Area = {bestParams.Item3}");
            
        //     int counter = 1;  // Starting counter
        //     foreach (var image in imageFile)
        //     {
        //         Image<Bgr, byte> srcImage = new Image<Bgr, byte>(image);
        //         var cleanedImage = ImageProcessor.CleanText(srcImage, bestParams.Item1, bestParams.Item2, bestParams.Item3);

        //         string cleanedImagePath = $"CleanedImage_{counter}.jpg";
        //         cleanedImage.Save(cleanedImagePath);

        //         counter++;  // Increment counter for next filename
        //     }
        // }

        // {   // OPTIMIZE BOX PARAMETERS

        //     // Define the path to your sample image
        //     string sampleImagePath = @"output_WPH1H0BYBYD_1.png";
        //     string csvPath = @"output_WPH1H0BYBYD_1.csv";

        //     // Set the correct box count for this image
        //     int correctBoxCount = 126; // Replace this with the actual number of boxes in your sample image

        //     // Run the optimization
        //     var bestParameters = ImageProcessor.OptimizeBoxParameters(sampleImagePath, csvPath);

        //     // Print the best parameters
        //     Console.WriteLine($"Best Block Size: {bestParameters.Item1}");
        //     Console.WriteLine($"Best C Value: {bestParameters.Item2}");
        //     Console.WriteLine($"Best Min Dimension: {bestParameters.Item3.Width}x{bestParameters.Item3.Height}");
        //     Console.WriteLine($"Best Max Dimension: {bestParameters.Item4.Width}x{bestParameters.Item4.Height}");
        // }

        // {   // FIX BOXES IN IMAGE
        //     string sampleImagePath = @"output_WPH1H0BYBYD_1.png";
        //     // ImageProcessor.FixBoxInImage(sampleImagePath);
        //     ImageProcessor.FixBoxAboveText(sampleImagePath, "E.", "F.");
        // }

        // {   // EXTRACT BOXES TO CSV
        //     ImageProcessor.ExtractBoxesToCSV(@"output_WPH1H0BYBYD_1.png");
        // }

        {   // Main program
            
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

                string text = File.ReadAllText(ocrResultsFile);

                // Dictionary<string, object> user = ExtractInfo(ocrResultsFile);
                Dictionary<string, object> user = new Dictionary<string, object>();


                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^1a.*?(\d{6,})", "ID");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^2.*?\)\s*([A-Z]+\s*,\s*[A-Z]+(?:\s*,\s*[A-Z])?)", "PatientName");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^3.*?(\d+).*?(\d+).*?(\d+)", "DateOfBirth");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^4.*?\)\s*([A-Z]+\s*,\s*[A-Z]+(?:\s*,\s*[A-Z])?)", "InsuredName");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^5.*?\)\s*(.+)$", "PatientAddress");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^5.*?(?:.*?\n){3}CITY\s+(.*?)(?=\n)", "PatientCity");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^5.*?(?:.*?\n){4}STATE\s+(.*?)(?=\n)", "PatientState");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^5.*?(?:.*?\n){10}ZIP CODE\s*(.*)", "PatientZip");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^5.*?(?:.*?\n){11}.*?\)\s*(.*)", "PatientTelephone");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^7.*?\)\s*(.+)$", "InsuredAddress");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^7.*?(?:.*?\n){4}CITY\s+(.*?)(?=\n)", "InsuredCity");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^7.*?(?:.*?\n){5}STATE\s+(.*?)(?=\n)", "InsuredState");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^7.*?(?:.*?\n){6}ZIP CODE\s*(.*)", "InsuredZip");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^7.*?(?:.*?\n){7}.*?\)\s*(.*)", "InsuredTelephone");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^11.*?(\d+)", "InsuredPolicyNumber");
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"^11.*?(?:.*?\n){2}.*?(\d+).*?(\d+).*?(\d+)", "InsuredDOB");
                
                
                PDFProcessor.AddFieldToDictionaryFromPattern(user, text, @"TOTAL CHARGE \$ ([0-9,.]+)", "TotalCharge");

                // Handling the diagnosis section using the list function
                bool isMatchFound = PDFProcessor.AddListFieldToDictionaryFromPattern(user, text, @"^(20\.|21\.)\s*.*?([A-Z0-9]{3}\.[A-Z0-9]{3,4})", "Diagnosis");

                if (!isMatchFound)
                {
                    PDFProcessor.AddListFieldToDictionaryFromPattern(user, text, @"^20\.[^\n]*\n.*?([A-Z0-9]{3}\.[A-Z0-9]{3,4})", "Diagnosis");
                }


                PDFProcessor.CleanDictionaryValues(user);


                {
                    try// 1a
                    {
                        Console.WriteLine($"ID: {user["ID"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("ID not found in the data.");
                    }

                    try// 2
                    {
                        Console.WriteLine($"Patient name: {user["PatientName"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("PatientName not found in the data.");
                    }

                    try// 3
                    {
                        Console.WriteLine($"Date of Birth: {user["DateOfBirth"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("DateOfBirth not found in the data.");
                    }
                    
                    try// 4
                    {
                        Console.WriteLine($"Insured name: {user["InsuredName"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("InsuredName not found in the data.");
                    }

                    try// 5
                    {
                        Console.WriteLine($"Patient address: {user["PatientAddress"]}");
                        Console.WriteLine($"Patient city: {user["PatientCity"]}");
                        Console.WriteLine($"Patient state: {user["PatientState"]}");
                        Console.WriteLine($"Patient zip: {user["PatientZip"]}");
                        Console.WriteLine($"Patient telephone: {user["PatientTelephone"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("PatientAddress not found in the data.");
                    }

                    try// 7
                    {
                        Console.WriteLine($"Insured address: {user["InsuredAddress"]}");
                        Console.WriteLine($"Insured city: {user["InsuredCity"]}");
                        Console.WriteLine($"Insured state: {user["InsuredState"]}");
                        Console.WriteLine($"Insured zip: {user["InsuredZip"]}");
                        Console.WriteLine($"Insured telephone: {user["InsuredTelephone"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("InsuredAddress not found in the data.");
                    }

                    try// 11
                    {
                        Console.WriteLine($"Insured policy number: {user["InsuredPolicyNumber"]}");
                        Console.WriteLine($"Insured DOB: {user["InsuredDOB"]}");
                    }
                    catch (KeyNotFoundException)
                    {
                        Console.WriteLine("InsuredPolicyNumber not found in the data.");
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
    }
}
