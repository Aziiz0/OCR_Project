using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Tesseract;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class ImageProcessor
{
    public static List<Tuple<string, Rectangle>> ExtractContours(string imagePath, int pageNumber, int blockSize, int cValue, Size minDimension, Size maxDimension)
    {
        List<Tuple<string, Rectangle>> contourData = new List<Tuple<string, Rectangle>>();

        // Create directory for cropped images if it doesn't exist
        var croppedImagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "CroppedImages");
        if (!Directory.Exists(croppedImagesDirectory)) Directory.CreateDirectory(croppedImagesDirectory);

        // Load the image
        using var src = new Image<Bgr, byte>(imagePath);

        // Convert the image to grayscale
        using var gray = src.Convert<Gray, byte>();

        // Adaptive Thresholding
        CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, blockSize, cValue);
        // CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 11, 2);

        // Find contours
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(gray, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

        // Define minimum and maximum dimensions
        // var minDimension = new Size((int)(src.Width / 30), (int)(src.Height / 35));  // set min width and height
        // var maxDimension = new Size((int)(src.Width / 1.4), (int)(src.Height / 3));  // set max width and height

        // Get all bounding rectangles
        List<Rectangle> boundingRects = new List<Rectangle>();
        for (int i = 0; i < contours.Size; i++)
        {
            boundingRects.Add(CvInvoke.BoundingRectangle(contours[i]));
        }

        // Sort the bounding rectangles from top-left to bottom-right
        boundingRects.Sort((r1, r2) => 
        {
            int result = r1.Y.CompareTo(r2.Y);
            if (result == 0) result = r1.X.CompareTo(r2.X);
            return result;
        });

        // Filter out duplicate or significantly overlapping bounding rectangles
        double IoUThreshold = 0.08;  // You can adjust this threshold based on your needs
        for (int i = boundingRects.Count - 1; i >= 0; i--)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if (ComputeIoU(boundingRects[i], boundingRects[j]) > IoUThreshold)
                {
                    boundingRects.RemoveAt(i);
                    break;
                }
            }
        }

        // Process each bounding rectangle
        for (int i = 0; i < boundingRects.Count; i++)
        {
            // Check if the rectangle is within the specified size range
            if (boundingRects[i].Width >= minDimension.Width && boundingRects[i].Height >= minDimension.Height
                && boundingRects[i].Width <= maxDimension.Width && boundingRects[i].Height <= maxDimension.Height)
            {
                // Crop the source image to the bounding rectangle
                var roi = new Mat(src.Mat, boundingRects[i]);

                // Convert the Mat to Image<Bgr, byte> and clean the cropped image
                var roiImage = roi.ToImage<Bgr, byte>();
                var cleanedRoi = CleanText(roiImage, 3, 1, 70);

                // Save the cleaned cropped image
                string contourImage = Path.Combine(croppedImagesDirectory, $"contour_{pageNumber}_{i}.png");
                try 
                {
                    cleanedRoi.Save(contourImage);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Error saving image: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }

                contourData.Add(new Tuple<string, Rectangle>(contourImage, boundingRects[i]));
            }
        }

        // If no contours were found, add the original image to the list
        if (contourData.Count == 0)
        {
            contourData.Add(new Tuple<string, Rectangle>(imagePath, Rectangle.Empty));
        }

        return contourData;
    }

    public static void ExtractBoxesToCSV(string imagePath)
    {
        using var src = new Image<Bgr, byte>(imagePath);
        using var gray = src.Convert<Gray, byte>();
        CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 11, 2);
        
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(gray, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
        
        List<Rectangle> boundingRects = new List<Rectangle>();
        for (int i = 0; i < contours.Size; i++)
        {
            boundingRects.Add(CvInvoke.BoundingRectangle(contours[i]));
        }

        // Write to CSV
        StringBuilder csvContent = new StringBuilder();
        csvContent.AppendLine("X,Y,Width,Height,ContentPath");

        string directoryPath = Path.GetDirectoryName(imagePath);
        string boxesFolderPath = Path.Combine(directoryPath, "ExtractedBoxes");
        
        // Create the directory if it doesn't exist
        if (!Directory.Exists(boxesFolderPath))
        {
            Directory.CreateDirectory(boxesFolderPath);
        }

        for (int i = 0; i < boundingRects.Count; i++)
        {
            var roi = new Mat(src.Mat, boundingRects[i]);
            string boxImagePath = Path.Combine(boxesFolderPath, $"box_{i}.png");
            roi.Save(boxImagePath);

            csvContent.AppendLine($"{boundingRects[i].X},{boundingRects[i].Y},{boundingRects[i].Width},{boundingRects[i].Height},{boxImagePath}");
        }

        string outputCSVPath = Path.ChangeExtension(imagePath, ".csv");
        File.WriteAllText(outputCSVPath, csvContent.ToString());
    }

    // Check if two rectangles match within a given leniency
    public static bool BoxesMatch(Rectangle a, Rectangle b, int leniency)
    {
        return Math.Abs(a.X - b.X) <= leniency &&
            Math.Abs(a.Y - b.Y) <= leniency &&
            Math.Abs(a.Width - b.Width) <= leniency &&
            Math.Abs(a.Height - b.Height) <= leniency;
    }

    public static (int, int, Size, Size) OptimizeBoxParameters(string imagePath, string csvPath)
    {
        // Load ground truth boxes from CSV
        var lines = File.ReadAllLines(csvPath).Skip(1);  // Skip header
        var groundTruthBoxes = lines.Select(line => {
            var parts = line.Split(',');
            return new Rectangle(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        }).ToList();

        // Define possible parameter values
        int[] blockSizes = { 3, 5, 7, 9, 11, 13, 15, 17 };
        int[] cValues = { -10, -8, -6, -4, -2, 0, 2 }; 
        double[] minWidthFactors = { 4, 6, 8, 10, 15, 20, 25 };
        double[] minHeightFactors = { 4, 6, 8, 10, 15, 20, 25 };
        double[] maxWidthFactors = { 0.5, 0.75, 1, 1.5, 2, 2.5, 3, 3.5, 4 };
        double[] maxHeightFactors = { 0.5, 0.75, 1, 1.5, 2, 2.5, 3, 3.5, 4 };

        int bestScore = 0;
        (int, int, Size, Size) bestParameters = (-1, -1, new Size(), new Size());

        using var src = new Image<Bgr, byte>(imagePath);

        object lockObject = new object();
        
        // // Directories for temporary and best matches
        // string bestDir = Path.Combine(Path.GetDirectoryName(imagePath), "BestMatches");
        // Directory.CreateDirectory(bestDir);
        // Prepare the log file
        string logFilePath = Path.Combine(Path.GetDirectoryName(imagePath), "comparison_log.txt");
        List<string> logEntries = new List<string>();

        // Create a list of all parameter combinations
        var parameterCombinations = from blockSize in blockSizes
                                    from cValue in cValues
                                    from minWidthFactor in minWidthFactors
                                    from minHeightFactor in minHeightFactors
                                    from maxWidthFactor in maxWidthFactors
                                    from maxHeightFactor in maxHeightFactors
                                    select new { blockSize, cValue, minWidthFactor, minHeightFactor, maxWidthFactor, maxHeightFactor };

        // Calculate the desired degree of parallelism (100% of available cores)
        int maxDegreeOfParallelism = (int)(0.8 * Environment.ProcessorCount);

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism };

        ConsoleProgressBar progressBar = new ConsoleProgressBar(parameterCombinations.Count());

        Parallel.ForEach(parameterCombinations, parallelOptions, (combination, loopState) =>
        {
            Size minDim = new Size((int)(src.Width / combination.minWidthFactor), (int)(src.Height / combination.minHeightFactor));
            Size maxDim = new Size((int)(src.Width / combination.maxWidthFactor), (int)(src.Height / combination.maxHeightFactor));

            var detectedData = ExtractContours(imagePath, 1, combination.blockSize, combination.cValue, minDim, maxDim);
            var detectedBoxes = detectedData.Select(data => data.Item2).Distinct().ToList();

            int matches = 0;

            List<Rectangle> unmatchedDetectedBoxes = new List<Rectangle>(detectedBoxes);

            foreach (var detectedBox in unmatchedDetectedBoxes)
            {
                foreach (var groundTruthBox in groundTruthBoxes)
                {
                    bool isMatch = BoxesMatch(groundTruthBox, detectedBox, 300);
                    // logEntries.Add($"Comparing DetectedBox: {detectedBox.ToString()} with GroundTruthBox: {groundTruthBox.ToString()} - Match: {isMatch}");
                    if (isMatch)
                    {
                        matches++;
                        break;
                    }
                }
            }

            lock (lockObject)
            {
                if (matches > bestScore)
                {
                    bestScore = matches;
                    bestParameters = (combination.blockSize, combination.cValue, minDim, maxDim);
                    
                    // If it's a perfect match, stop the parallel loop
                    if (matches == groundTruthBoxes.Count)
                    {
                        loopState.Stop();
                    }
                }
                progressBar.IncrementStep(bestScore.ToString());
            }
        });

        Console.WriteLine($"Best score: {bestScore}");
        // Write log entries to file
        // File.WriteAllLines(logFilePath, logEntries);

        return bestParameters;
    }

    public static double ComputeIoU(Rectangle boxA, Rectangle boxB)
    {
        int xA = Math.Max(boxA.X, boxB.X);
        int yA = Math.Max(boxA.Y, boxB.Y);
        int xB = Math.Min(boxA.X + boxA.Width, boxB.X + boxB.Width);
        int yB = Math.Min(boxA.Y + boxA.Height, boxB.Y + boxB.Height);

        int interArea = Math.Max(0, xB - xA + 1) * Math.Max(0, yB - yA + 1);
        int boxAArea = boxA.Width * boxA.Height;
        int boxBArea = boxB.Width * boxB.Height;

        return (double)interArea / (boxAArea + boxBArea - interArea);
    }

    public enum TextAlignment
    {
        Center,
        Left
    }

    public static void FixBoxAboveText(string imagePath, string first, string second, TextAlignment textAlignment = TextAlignment.Center, int lineThickness = 2, int heightAboveText = 12, int lineWidth = 260)
    {
        using (Image<Bgr, byte> img = new Image<Bgr, byte>(imagePath))
        using (Image<Gray, byte> grayImg = img.Convert<Gray, byte>())
        {
            // Use Tesseract to locate the position of the text
            using (var ocr = new TesseractEngine(@"tessdata", "eng", EngineMode.Default))
            using (var page = ocr.Process(Pix.LoadFromFile(imagePath)))
            {
                // Save the extracted text to a file
                // string extractedText = page.GetText();
                // File.WriteAllText(Path.Combine(Path.GetDirectoryName(imagePath), "extractedText.txt"), extractedText);

                var iterator = page.GetIterator();
                iterator.Begin();

                do
                {
                    if (iterator.GetText(PageIteratorLevel.Word) == first)
                    {
                        Rect currentWordBounds;
                        if (iterator.TryGetBoundingBox(PageIteratorLevel.Word, out currentWordBounds))
                        {
                            if (iterator.Next(PageIteratorLevel.Word) && iterator.GetText(PageIteratorLevel.Word) == second)
                            {
                                int x1 = currentWordBounds.X1;
                                int y1 = currentWordBounds.Y1 - heightAboveText;  // Start above the detected text
                                int x2 = x1 + lineWidth;

                                if (textAlignment == TextAlignment.Center)
                                {
                                    int shiftAmount = lineWidth / 2;
                                    x1 -= shiftAmount;
                                    x2 -= shiftAmount;
                                }

                                img.Draw(new LineSegment2D(new Point(x1, y1), new Point(x2, y1)), new Bgr(0, 0, 0), lineThickness);
                                img.Save(imagePath);
                                return;
                            }
                        }
                    }
                } while (iterator.Next(PageIteratorLevel.Word));
            }
        }

        Console.WriteLine($"Combination of '{first}' followed by '{second}' not found in the image.");
    }

    // enum Direction { None, Down, Right, Up, Left }

    // public static void FixBoxInImage(string imagePath)
    // {
    //     Image<Bgr, byte> img = new Image<Bgr, byte>(imagePath);

    //     using (img)
    //     {
    //         // Convert to grayscale for processing
    //         Image<Gray, byte> grayImg = img.Convert<Gray, byte>().ThresholdBinary(new Gray(128), new Gray(255));

    //         int width = grayImg.Width;
    //         int height = grayImg.Height;

    //         Console.WriteLine($"Image Width: {width}, Height: {height}");

    //         // Step 1: Find the first sufficiently long horizontal line near the top of the image
    //         int startingRow = -1;
    //         int maxLengthThreshold = width / 3;  // Assuming a line is "long" if it's at least 1/3 of the image width

    //         for (int i = 200; i < height / 4; i++)  // Assume the line is in the top 25% of the image
    //         {
    //             int blackCount = 0;
    //             for (int j = 0; j < width; j++)
    //             {
    //                 if (grayImg.Data[i, j, 0] == 0) // Assuming lines are black
    //                 {
    //                     blackCount++;
    //                 }
    //             }
    //             if (blackCount > maxLengthThreshold)
    //             {
    //                 startingRow = i;
    //                 break;
    //             }
    //         }

    //         if (startingRow == -1)
    //         {
    //             Console.WriteLine("No suitable starting row found.");
    //             return;
    //         }
    //         Console.WriteLine($"Starting row found at: {startingRow}");

    //         // Find the leftmost edge of this line
    //         int startingX = 0;
    //         while (grayImg.Data[startingRow, startingX, 0] != 0 && startingX < width - 1)
    //         {
    //             startingX++;
    //         }
    //         Console.WriteLine($"Starting X position: {startingX}");


    //         // To keep track of visited intersections
    //         HashSet<Point> visitedIntersections = new HashSet<Point>();

    //         int x = startingX;
    //         int y = startingRow;

    //         int thresholdX = 0;
    //         int thresholdY = 0;
            
    //         Stack<Point> stack = new Stack<Point>();
    //         HashSet<Point> visited = new HashSet<Point>();
    //         Bgr redColor = new Bgr(0, 0, 255);

    //         stack.Push(new Point(startingX, startingRow));

    //         Direction currentDirection = Direction.Down;  // We start by moving downwards

    //         while (stack.Count > 0)
    //         {
    //             Point current = stack.Pop();

    //             if (visited.Contains(current) || current.X < 0 || current.X >= width || current.Y < 0 || current.Y >= height)
    //             {
    //                 continue;  // skip if the point was visited before or is outside the image boundaries
    //             }

    //             visited.Add(current);

    //             // Mark the pixel in red
    //             img[current.Y, current.X] = redColor;

    //             // Based on the current direction, decide which way to go
    //             switch (currentDirection)
    //             {
    //                 case Direction.Down:
    //                     if (!visited.Contains(new Point(current.X, current.Y + 1)) && grayImg.Data[current.Y + 1, current.X, 0] == 0 && Math.Abs(current.Y - startingRow) >= thresholdY)
    //                     {
    //                         stack.Push(new Point(current.X, current.Y + 1));
    //                     }
    //                     else
    //                     {
    //                         currentDirection = Direction.Right;  // Change direction to right
    //                     }
    //                     break;

    //                 case Direction.Right:
    //                     if (!visited.Contains(new Point(current.X + 1, current.Y)) && grayImg.Data[current.Y, current.X + 1, 0] == 0 && Math.Abs(current.X - startingX) >= thresholdX)
    //                     {
    //                         stack.Push(new Point(current.X + 1, current.Y));
    //                     }
    //                     else
    //                     {
    //                         currentDirection = Direction.Up;  // Change direction to up
    //                     }
    //                     break;

    //                 case Direction.Up:
    //                     if (!visited.Contains(new Point(current.X, current.Y - 1)) && grayImg.Data[current.Y - 1, current.X, 0] == 0 && Math.Abs(current.Y - startingRow) >= thresholdY)
    //                     {
    //                         stack.Push(new Point(current.X, current.Y - 1));
    //                     }
    //                     else
    //                     {
    //                         currentDirection = Direction.Left;  // Change direction to left
    //                     }
    //                     break;

    //                 case Direction.Left:
    //                     if (!visited.Contains(new Point(current.X - 1, current.Y)) && grayImg.Data[current.Y, current.X - 1, 0] == 0 && Math.Abs(current.X - startingX) >= thresholdX)
    //                     {
    //                         stack.Push(new Point(current.X - 1, current.Y));
    //                     }
    //                     break;
    //             }
    //         }

    //         // Save the modified image
    //         img.Save(imagePath);
    //     }
    // }

    public static List<int> DetectCheckBoxes(string imagePath, double densityThreshold = 0.4)
    {
        List<Rectangle> checkBoxes = new List<Rectangle>();

        // Load the image
        using var src = new Image<Bgr, byte>(imagePath);

        // Convert the image to grayscale
        using var gray = src.Convert<Gray, byte>();

        // Adaptive Thresholding
        CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 11, 2);

        // Noise Reduction
        Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
        CvInvoke.MorphologyEx(gray, gray, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

        // Find contours
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(gray, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

        // Set a fixed size for the checkboxes
        Size fixedCheckBoxSize = new Size(70, 70); // Adjust based on your checkboxes
        int sizeTolerance = 10; // Tolerance for checkbox size

        for (int i = 0; i < contours.Size; i++)
        {
            var boundingRect = CvInvoke.BoundingRectangle(contours[i]);

            // Calculate aspect ratio, area, and solidity
            double aspectRatio = (double)boundingRect.Width / boundingRect.Height;
            double contourArea = CvInvoke.ContourArea(contours[i]);
            double solidity = contourArea / (boundingRect.Width * boundingRect.Height);

            // Use contour approximation to check if the contour is nearly a rectangle
            VectorOfPoint approxContour = new VectorOfPoint();
            CvInvoke.ApproxPolyDP(contours[i], approxContour, CvInvoke.ArcLength(contours[i], true) * 0.06, true);  // Adjusted to 0.05

            // Specify a fixed area range for the checkboxes.
            int minArea = (fixedCheckBoxSize.Width - sizeTolerance) * (fixedCheckBoxSize.Height - sizeTolerance);
            int maxArea = (fixedCheckBoxSize.Width + sizeTolerance) * (fixedCheckBoxSize.Height + sizeTolerance);

            if (aspectRatio >= 0.85 && aspectRatio <= 1.15 &&   // Adjusted aspect ratio range
                solidity > 0.90 &&                             // Reduced solidity threshold
                approxContour.Size == 4 && 
                contourArea >= minArea && contourArea <= maxArea)
            {
                // Flag to determine if the current boundingRect should be added to checkBoxes
                bool shouldAdd = true;

                // Check against already detected boxes
                for (int j = 0; j < checkBoxes.Count; j++)
                {
                    // If boundingRect is contained within a box already in the list, remove the outer box and add boundingRect
                    if (checkBoxes[j].Contains(boundingRect.Location) && checkBoxes[j].Contains(new Point(boundingRect.Right, boundingRect.Bottom)))
                    {
                        checkBoxes.RemoveAt(j);
                        j--; // Adjust the index after removing an item
                    }
                    // If boundingRect contains a box already in the list, don't add boundingRect
                    else if (boundingRect.Contains(checkBoxes[j].Location) && boundingRect.Contains(new Point(checkBoxes[j].Right, checkBoxes[j].Bottom)))
                    {
                        shouldAdd = false;
                        break;
                    }
                }

                if (shouldAdd)
                {
                    checkBoxes.Add(boundingRect);
                    Console.WriteLine($"Found checkbox at ({boundingRect.X}, {boundingRect.Y})");
                }
            }

            approxContour.Dispose();
        }

        // If checkboxes were detected, then save the debug images
        if (checkBoxes.Count > 0)
        {
            // Debugging: Create a separate directory for debug images if it doesn't exist
            string debugDir = Path.Combine(Path.GetDirectoryName(imagePath), "DebugImages");
            if (!Directory.Exists(debugDir))
            {
                Directory.CreateDirectory(debugDir);
            }

            // Save the regions of detected checkboxes
            string debugImagePath = Path.Combine(debugDir, "debug_" + Path.GetFileName(imagePath));
            foreach (var boxRect in checkBoxes)
            {
                // Highlight the detected checkbox with a red rectangle
                src.Draw(boxRect, new Bgr(0, 0, 255), 2);
            }
            src.Save(debugImagePath);
            Console.WriteLine($"Debug image saved to: {debugImagePath}");
        }

        // Call the GetCheckedBoxes function
        return GetCheckedBoxes(imagePath, checkBoxes, densityThreshold);
    }

    private static List<int> GetCheckedBoxes(string imageFile, List<Rectangle> checkBoxRectangles, double densityThreshold = 0.4)
    {
        List<int> checkedBoxes = new List<int>();

        // Define a tolerance based on the height of the checkboxes or other relevant metric
        int yTolerance = 10; // You can adjust this value as needed

        checkBoxRectangles.Sort((r1, r2) =>
        {
            if (Math.Abs(r1.Y - r2.Y) <= yTolerance) // If the boxes are roughly on the same row
            {
                return r1.X.CompareTo(r2.X); // Sort by X-coordinate (left to right)
            }
            return r1.Y.CompareTo(r2.Y); // Otherwise, sort by Y-coordinate (top to bottom)
        });

        using (var src = new Image<Gray, byte>(imageFile))
        {
            Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));

            for (int i = 0; i < checkBoxRectangles.Count; i++)
            {
                var roi = new Mat(src.Mat, checkBoxRectangles[i]);

                // Erode the ROI to focus more on the internal area
                CvInvoke.Erode(roi, roi, kernel, new Point(-1, -1), 1, BorderType.Default, new MCvScalar());

                double pixelDensity = CvInvoke.CountNonZero(roi) / (double)(roi.Width * roi.Height);
                    
                if (pixelDensity < densityThreshold)
                {
                    checkedBoxes.Add(i + 1);
                }
            }
        }
        return checkedBoxes;
    }

    public static Image<Bgr, byte> CleanText(Image<Bgr, byte> src, int kernelSize, int iterations, double thresholdArea)
    {
        Image<Gray, byte> gray = src.Convert<Gray, byte>();
        Image<Gray, byte> thresh = gray.ThresholdBinaryInv(new Gray(0), new Gray(255));

        // Dilation
        Mat kernel = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(kernelSize, kernelSize), new Point(-1, -1)); // kernel size (less is more lenient)
        Image<Gray, byte> dilate = thresh.MorphologyEx(MorphOp.Dilate, kernel, new Point(-1, -1), iterations, BorderType.Default, new MCvScalar()); // iterations (less is more lenient)

        // Define the region to ignore (e.g., top left part of the image)
        Rectangle ignoreRegion = new Rectangle(0, 0, src.Width / 8, src.Height / 4); // You can adjust the width and height accordingly

        // Find contours and filter using a minimum threshold area
        VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
        Mat hierarchy = new Mat();
        CvInvoke.FindContours(dilate, contours, hierarchy, RetrType.Tree, ChainApproxMethod.ChainApproxSimple);
        
        Image<Gray, byte> mask = new Image<Gray, byte>(src.Width, src.Height, new Gray(0)); // Initialize with black mask
        for (int i = 0; i < contours.Size; i++)
        {
            Rectangle boundingRect = CvInvoke.BoundingRectangle(contours[i]);

            // If the contour's bounding rectangle intersects with the ignore region, skip it
            if (!boundingRect.IntersectsWith(ignoreRegion))
            {
                if (CvInvoke.ContourArea(contours[i]) < thresholdArea) // threshold area (less is more strict)
                {
                    CvInvoke.DrawContours(mask, contours, i, new MCvScalar(255), -1); // Draw contours in white
                }
            }
        }

        Image<Bgr, byte> result = src.Copy();
        result.SetValue(new Bgr(255, 255, 255), mask);

        return result;
    }

    private static Dictionary<string, string> referenceTexts = new Dictionary<string, string>
    {
        { @"CroppedImages\contour_1_466.png", "1a. INSURED'S I.D. NUMBER (For Program in Item 1) 592463114" },
        { @"CroppedImages\contour_1_724.png", "2. PATIENT'S NAME (Last Name, First Name, Middle Initial) WHITE, EDGAR" },
        { @"CroppedImages\contour_1_1770.png", "b. OTHER CLAIM ID (Designated by NUCC) Y4 230038965" },
        { @"CroppedImages\contour_1_3983.png", "12 29 22" },
        { @"CroppedImages\contour_1_3986.png", "A0427 SH" },
        { @"CroppedImages\contour_1_3987.png", "A 978.00" }
        // ... add all image paths and their corresponding reference texts
    };

    private static int EvaluateOCRQuality(TesseractEngine engine, string cleanedImagePath, string originalImagePath)
    {
        if (!referenceTexts.ContainsKey(originalImagePath))
        {
            Console.WriteLine($"Warning: No reference text found for {originalImagePath}. Defaulting to an empty string.");
            return int.MaxValue;  // Return a large value to indicate a "bad" result
        }

        string referenceText = referenceTexts[originalImagePath];
        string ocrText = PDFProcessor.PageOCR(engine, cleanedImagePath);
        return ComputeWeightedErrorPercentage(ocrText, referenceText);
    }

    public static (int, int, double) OptimizeCleanTextParameters(string dataPath, string language, List<string> imageFiles)
    {
        int[] kernelSizes = {1, 2, 3, 4};
        int[] iterations = {1, 2, 3, 4};
        double[] thresholdAreas = {50, 100, 1000, 2000, 3000};

        int bestWordCount = int.MaxValue;
        (int, int, double) bestParameters = (-1, -1, -1);

        foreach (var kernel in kernelSizes)
        {
            foreach (var iteration in iterations)
            {
                foreach (var threshold in thresholdAreas)
                {
                    int totalWordCount = 0;

                    Parallel.ForEach(imageFiles, (imageFile) =>
                    {
                        // Create a unique temporary file name for each thread
                        string tempFileName = $"temp_cleaned_image_{Guid.NewGuid()}.png";

                        using (var engine = new TesseractEngine(dataPath, language, EngineMode.Default))
                        {
                            Image<Bgr, byte> cleanedImage = CleanText(new Image<Bgr, byte>(imageFile), kernel, iteration, threshold);
                            cleanedImage.Save(tempFileName);
                                                            
                            int wordCount = EvaluateOCRQuality(engine, tempFileName, imageFile);
                            Interlocked.Add(ref totalWordCount, wordCount);

                            Console.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Processed {imageFile}");

                            // Optional: Delete the temporary cleaned image
                            File.Delete(tempFileName);
                        }
                    });

                    int averageWordCount = totalWordCount / imageFiles.Count;

                    Console.WriteLine($"Kernel: {kernel}, Iteration: {iteration}, Threshold: {threshold} => Average Word Count: {averageWordCount}");

                    if (averageWordCount > 0 && averageWordCount < bestWordCount)
                    {
                        bestWordCount = averageWordCount;
                        bestParameters = (kernel, iteration, threshold);

                        Console.WriteLine($"[Info] New best parameters found: Kernel: {kernel}, Iteration: {iteration}, Threshold: {threshold} with Average Word Count: {averageWordCount}");
                    }
                }
            }
        }

        Console.WriteLine($"Best Parameters: Kernel Size = {bestParameters.Item1}, Iterations = {bestParameters.Item2}, Threshold Area = {bestParameters.Item3}");
        return bestParameters;
    }

    public static int ComputeLevenshteinDistance(string s, string t)
    {
        int[,] dp = new int[s.Length + 1, t.Length + 1];

        for (int i = 0; i <= s.Length; i++)
            dp[i, 0] = i;

        for (int j = 0; j <= t.Length; j++)
            dp[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                if (s[i - 1] == t[j - 1])
                    dp[i, j] = dp[i - 1, j - 1];
                else
                    dp[i, j] = Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1])) + 1;
            }
        }

        return dp[s.Length, t.Length];
    }

    public static int ComputeWeightedErrorPercentage(string s, string t)
    {
        int levenshteinDistance = ComputeLevenshteinDistance(s, t);
        
        double errorPercentage = (double)levenshteinDistance / s.Length * 100;
        double weight = 1.0 / s.Length;  // Inverse weighting based on reference string length

        return (int)Math.Round(errorPercentage * weight);
    }

    public static void DetectTopLeftCorners(string imagePath, int minLength, string debugDir = "DebugImages")
    {
        // Load the image
        using var src = new Image<Bgr, byte>(imagePath);

        // Convert the image to grayscale for processing
        using var gray = src.Convert<Gray, byte>();

        // Use Canny Edge detection to get edges in the image
        using var edges = gray.Canny(50, 150);

        for (int y = 0; y < edges.Rows - minLength; y++)
        {
            for (int x = 0; x < edges.Cols - minLength; x++)
            {
                // Check for the top-left corner
                bool isCorner = true;
                for (int i = 0; i < minLength; i++)
                {
                    if (edges.Data[y + i, x, 0] == 0 || edges.Data[y, x + i, 0] == 0)
                    {
                        isCorner = false;
                        break;
                    }
                }

                if (isCorner)
                {
                    // Draw the corner in red
                    src.Draw(new Rectangle(x, y, minLength, minLength), new Bgr(Color.Red), 2);

                    // Move the search window to avoid overlapping corners
                    x += minLength;
                }
            }
        }

        // Draw the minimum threshold circle in blue
        src.Draw(new CircleF(new PointF(minLength, minLength), minLength), new Bgr(Color.Blue), 2);

        // Save the result to the debug directory
        if (!Directory.Exists(debugDir))
        {
            Directory.CreateDirectory(debugDir);
        }
        string debugImagePath = Path.Combine(debugDir, "debug_" + Path.GetFileName(imagePath));
        src.Save(debugImagePath);
        Console.WriteLine($"Debug image saved to: {debugImagePath}");
    }
}