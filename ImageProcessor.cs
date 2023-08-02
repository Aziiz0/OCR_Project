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

        // Threshold the image to get a binary image
        CvInvoke.Threshold(gray, gray, 127, 255, ThresholdType.Binary);

        // Find contours
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(gray, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

        // Define minimum and maximum dimensions
        var minDimension = new Size(90, 90);  // change these as needed
        var maxDimension = new Size(2500, 1000);  // change these as needed

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

        return contourImages;
    }
}
