using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

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
        var minDimension = new Size((int)(src.Width / 11), (int)(src.Height / 35));  // set min width and height
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
}