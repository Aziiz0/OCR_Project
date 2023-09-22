using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Tesseract;

public class ImageProcessor
{
    public static List<string> ExtractContours(string imagePath, int pageNumber)
    {
        List<string> contourImages = new List<string>();
        
        // Create directory for cropped images if it doesn't exist
        var croppedImagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "CroppedImages");
        if (!Directory.Exists(croppedImagesDirectory)) Directory.CreateDirectory(croppedImagesDirectory);

        // Load the image
        using var src = new Image<Bgr, byte>(imagePath);

        // Convert the image to grayscale
        using var gray = src.Convert<Gray, byte>();

        // Adaptive Thresholding
        CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 11, 2);

        // Find contours
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(gray, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

        // Define minimum and maximum dimensions
        var minDimension = new Size((int)(src.Width / 30), (int)(src.Height / 35));  // set min width and height
        var maxDimension = new Size((int)(src.Width / 1.4), (int)(src.Height / 3));  // set max width and height

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

        // Process each bounding rectangle
        for (int i = 0; i < boundingRects.Count; i++)
        {
            // Check if the rectangle is within the specified size range
            if (boundingRects[i].Width >= minDimension.Width && boundingRects[i].Height >= minDimension.Height
                && boundingRects[i].Width <= maxDimension.Width && boundingRects[i].Height <= maxDimension.Height)
            {
                // Crop the source image to the bounding rectangle
                var roi = new Mat(src.Mat, boundingRects[i]);

                // Save the cropped image
                string contourImage = Path.Combine(croppedImagesDirectory, $"contour_{pageNumber}_{i}.png");
                CvInvoke.Imwrite(contourImage, roi);

                contourImages.Add(contourImage);
            }
        }

        // If no contours were found, add the original image to the list
        if (contourImages.Count == 0)
        {
            contourImages.Add(imagePath);
        }

        return contourImages;
    }

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
            CvInvoke.ApproxPolyDP(contours[i], approxContour, CvInvoke.ArcLength(contours[i], true) * 0.05, true);  // Adjusted to 0.05

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