using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine;
using UnityEngine.UI;
using System;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using UnityEngine.XR.ARSubsystems;
using OpenCVForUnity.UnityUtils;
using Unity.VisualScripting.Antlr3.Runtime;
using Unity.VisualScripting;
using TMPro.Examples;
using System.Linq;

public class OpenCVTest : MonoBehaviour
{
    public ARCameraManager cameraManager;
    public ARPlaneManager planeManager;
    public Camera _camera;
    private string imagePath = "Debug/64";
    private int frameCount = 0;
    private float totalTime = 0;
    private float minTime = 999;
    private Vector3[] corner3DPoints;
    private float maxTime = 0;
    public Text text;
    // public RawImage rawImage;
    // private List<GameObject> pointObjects = new List<GameObject>();
    // public GameObject pointPrefab;


    void Start()
    {
        // if (rawImage == null)
        // {
        //     Debug.LogError("RawImage component not found");
        //     return;
        // }
        // Texture2D texture = Resources.Load<Texture2D>(imagePath);
        // if (texture != null)
        // {
        //     Debug.Log("Successed to load image: " + imagePath);
        //     Debug.Log("Texture Size: " + texture.width + " x " + texture.height);
        //     Debug.Log("RawImage Size: " + rawImage.rectTransform.rect.width + " x " + rawImage.rectTransform.rect.height);
        //     Mat originMat = new Mat(texture.height, texture.width, CvType.CV_8UC4);
        //     Utils.texture2DToMat(texture, originMat);
        //     List<MatOfPoint> contours = GetContours(originMat);
        //     List<List<Point>> corners = calc_cornerpoint_for_CorntoursLine(contours, originMat.width(), originMat.height());
        //     foreach (var cornerSet in corners)
        //     {
        //         foreach (var corner in cornerSet)
        //         {
        //             Imgproc.circle(originMat, corner, 8, new Scalar(255, 255, 0), -1);
        //         }
        //     }
        //     Texture2D resultTexture = new Texture2D(originMat.cols(), originMat.rows(), TextureFormat.RGBA32, false);
        //     Utils.matToTexture2D(originMat,resultTexture);
        //     rawImage.texture = resultTexture;
        //     // rawImage.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);
        // }
        // else
        // {
        //     Debug.LogError("Failed to load image: " + imagePath);
        // }
    }


    void Update()
    {
        // Clear3DPoints();
        frameCount += 1;
        if (cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            using (image)
            {
                Mat imageMat = ConvertXRCpuImageToMat(image);
                float startTime = Time.realtimeSinceStartup;
                List<MatOfPoint> contours = GetContours(imageMat);
                List<List<Point>> corners = calc_cornerpoint_for_CorntoursLine(contours, imageMat.width(), imageMat.height());
                float endTime = Time.realtimeSinceStartup;
                float executeTime = endTime - startTime;
                totalTime += executeTime;
                if (executeTime < minTime)
                {
                    minTime = executeTime;
                }
                if (executeTime > maxTime)
                {
                    maxTime = executeTime;
                }
                Point[] points = corners.SelectMany(list => list).ToArray();
                corner3DPoints = ConvRGBCameraPointsToVector3(
                    points
                );
                Debug.Log($"average execute time : {totalTime / frameCount} s/frame");
                Debug.Log($"min execute time : {minTime} s/frame");
                Debug.Log($"max execute time : {maxTime} s/frame");
                // foreach (var cornerSet in corners)
                // {
                //     foreach (var corner in cornerSet)
                //     {
                //         Imgproc.circle(imageMat, corner, 8, new Scalar(255, 255, 0), -1);
                //     }
                // }
                // Texture2D resultTexture = new Texture2D(imageMat.cols(), imageMat.rows(), TextureFormat.RGBA32, false);
                // Utils.matToTexture2D(imageMat, resultTexture);
                // rawImage.texture = resultTexture;
                if(corner3DPoints != null)
                {
                    // _view.Draw3DPoints(_view.Corner3DPoints.Select(p => new Vector3(p.x, p.y, p.z)).ToList());
                    foreach(Vector3 point in corner3DPoints)
                    {
                        Render3DPoint(point);
                    }
                }
                image.Dispose();
                // Destroy(resultTexture);
                SetText($"average : {totalTime / frameCount} s/f; min : {minTime} s/f; max  : {maxTime} s/f");

            }
        }
    }
    public void Render3DPoint(Vector3 position, Color? color = null)
    {
        GameObject point = GameObject.CreatePrimitive(PrimitiveType.Cube);
        point.transform.position = position;
        point.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        point.GetComponent<Renderer>().material.color = color ?? Color.blue;
        point.tag = "Cad3DPoint";
    }
    private float GetMinHeightOfARPlanes()
    {
        float minHeight = 0;
        foreach (var plane in planeManager.trackables)
        {
            if (plane is ARPlane arPlane)
            {
                if (arPlane.alignment == PlaneAlignment.HorizontalUp)
                {
                    if (minHeight == 0 || minHeight > arPlane.transform.position.y)
                    {
                        minHeight = arPlane.transform.position.y;
                    }
                }
            }
        }
        return minHeight;
    }
    public Vector3[] ConvRGBCameraPointsToVector3(
           Point[] pointsOnRGBCameraFrame,
           float offsetY = 0f
       )
    {
        if (cameraManager.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
        {
            List<Vector3> resultList = new();

            float fx = cameraIntrinsics.focalLength.x;
            float fy = cameraIntrinsics.focalLength.y;
            float cx = cameraIntrinsics.principalPoint.x;
            float cy = cameraIntrinsics.principalPoint.y;

            float planeHeight = GetMinHeightOfARPlanes();

            Plane groundPlane = new(Vector3.up, new Vector3(0, planeHeight + offsetY, 0));

            for (int i = 0; i < pointsOnRGBCameraFrame.Length; i++)
            {
                Point pixel = pointsOnRGBCameraFrame[i];

                double x = (pixel.y - cx) / fx;
                double y = (pixel.x - cy) / fy;


                Vector3 cameraDirection = new Vector3((float)x, (float)y, 1).normalized;

                Vector3 worldDirection = _camera.transform.TransformDirection(cameraDirection);
                Vector3 cameraPosition = _camera.transform.position;


                Ray ray = new(cameraPosition, worldDirection);


                if (groundPlane.Raycast(ray, out float distance))
                {

                    Vector3 hitPoint = ray.GetPoint(distance);
                    resultList.Add(hitPoint);
                }
            }
            return resultList.ToArray();
        }
        else
        {
            Debug.LogError("Failed to get camera intrinsics in EdgePointsView.ConvRGBCameraPointsToVector3");
            return null;
        }

    }

    public void Clear3DPoints()
    {
        foreach (GameObject point in GameObject.FindGameObjectsWithTag("Cad3DPoint"))
        {
            Destroy(point.GetComponent<Renderer>().material);
            Destroy(point);
        }
    }
    public void SetText(string s)
    {
        text.text = s;
    }
    public Mat ConvertXRCpuImageToMat(XRCpuImage image)
    {
        Mat mat = new Mat(image.height, image.width, CvType.CV_8UC4);
        XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, XRCpuImage.Transformation.None);
        image.Convert(conversionParams, (IntPtr)mat.dataAddr(), (int)mat.total() * (int)mat.elemSize());
        return mat;
    }

    public (double, double) calc_a_b(double x1, double y1, double x2, double y2)
    {
        double a = (y2 - y1) / (x2 - x1);
        double b = y1 - a * x1;
        return (a, b);
    }

    public List<(double, double)> merge_accelaretons(List<(double, double)> accelaretons, double threshold_slope = 10, double threshold_intercept = 100)
    {
        accelaretons.Sort((x, y) => x.Item1 == y.Item1 ? x.Item2.CompareTo(y.Item2) : x.Item1.CompareTo(y.Item1));
        var resultaccelaretons = new List<(double, double)>();

        foreach (var aa in accelaretons)
        {
            if (resultaccelaretons.Count == 0)
            {
                resultaccelaretons.Add(aa);
            }
            else
            {
                var last = resultaccelaretons[resultaccelaretons.Count - 1];
                if (Mathf.Abs((float)(last.Item1 - aa.Item1)) < threshold_slope && Mathf.Abs((float)(last.Item2 - aa.Item2)) < threshold_intercept)
                {
                    resultaccelaretons[resultaccelaretons.Count - 1] = ((last.Item1 + aa.Item1) / 2, (last.Item2 + aa.Item2) / 2);
                }
                else
                {
                    resultaccelaretons.Add(aa);
                }
            }
        }
        return resultaccelaretons;
    }

    public List<List<Point>> calc_cornerpoint_for_CorntoursLine(List<MatOfPoint> contours, int width, int height)
    {
        var resultcorners = new List<List<Point>>();


        foreach (var contour in contours)
        {
            double epsilon = 0.02 * Imgproc.arcLength(new MatOfPoint2f(contour.toArray()), true);
            var approx = new MatOfPoint2f();
            Imgproc.approxPolyDP(new MatOfPoint2f(contour.toArray()), approx, epsilon, true);
            if (approx.total() != 4)
                continue;

            var result = Mat.zeros(height, width, CvType.CV_8UC1);
            Imgproc.drawContours(result, new List<MatOfPoint> { contour }, -1, new Scalar(255), 1);

            var lines = new Mat();
            Imgproc.HoughLinesP(result, lines, 1, Mathf.PI / 180, 10, 20, 20);
            if (lines.rows() == 0)
                continue;

            var accelaretons = new List<(double, double)>();
            for (int i = 0; i < lines.rows(); i++)
            {
                double[] line = lines.get(i, 0);
                double x1 = line[0], y1 = line[1], x2 = line[2], y2 = line[3];
                accelaretons.Add(calc_a_b(x1, y1, x2, y2));
            }

            accelaretons = merge_accelaretons(accelaretons);
            if (accelaretons.Count != 4)
                continue;

            var corners_ = new List<Point>();
            var combinations = new List<(int, int)> { (0, 2), (0, 3), (1, 2), (1, 3) };

            foreach (var (i, j) in combinations)
            {
                var (a1, b1) = accelaretons[i];
                var (a2, b2) = accelaretons[j];

                var A = new Mat(2, 2, CvType.CV_64F);
                A.put(0, 0, a1);
                A.put(0, 1, -1);
                A.put(1, 0, a2);
                A.put(1, 1, -1);

                var B = new Mat(2, 1, CvType.CV_64F);
                B.put(0, 0, -b1);
                B.put(1, 0, -b2);

                if (Core.determinant(A) == 0)
                    continue;

                var intersection = new Mat();
                Core.gemm(A.inv(), B, 1, new Mat(), 0, intersection);

                double x = intersection.get(0, 0)[0];
                double y = intersection.get(1, 0)[0];

                if (double.IsNaN(x) || double.IsNaN(y))
                    break;

                if (x < 0 || x > width || y < 0 || y > height)
                    continue;

                corners_.Add(new Point((int)x, (int)y));
            }

            if (corners_.Count == 4)
                resultcorners.Add(corners_);
        }

        return resultcorners;
    }






    public Texture2D ReadImageFromResource(string filePath)
    {
        Texture2D texture = Resources.Load(filePath) as Texture2D;
        if (texture == null)
        {
            Debug.LogError("cannot load from : " + filePath);
            return null;
        }
        return texture;
    }

    public Mat texture2DToOpenCVMat(Texture2D texture)
    {
        Mat dstMat = new Mat(texture.height, texture.width, CvType.CV_8UC4);
        Utils.texture2DToMat(texture, dstMat);
        Imgproc.cvtColor(dstMat, dstMat, Imgproc.COLOR_RGBA2BGRA);
        return dstMat;
    }

    // public void DisplayResult(Mat result)
    // {
    //     Mat grayMat = new Mat();
    //     Imgproc.cvtColor(result, grayMat, Imgproc.COLOR_BGRA2GRAY);
    //     Texture2D resultTexture = new Texture2D(result.cols(), result.rows(), TextureFormat.RGB24, false);
    //     Utils.matToTexture2D(grayMat, resultTexture);

    //     rawImage.texture = resultTexture;
    //     rawImage.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);

    // }
    public List<MatOfPoint> GetContours(Mat rgbaMat, int percentile = 80)
    {
        Vector2[] a = new Vector2[0];
        if (rgbaMat == null || rgbaMat.empty())
            return null;


        Mat grayMat = new Mat();
        Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);


        Mat scharrX = new Mat();
        Mat scharrY = new Mat();
        Imgproc.Scharr(grayMat, scharrX, CvType.CV_64F, 1, 0);
        Imgproc.Scharr(grayMat, scharrY, CvType.CV_64F, 0, 1);


        Mat scharr = new Mat();
        Core.magnitude(scharrX, scharrY, scharr);


        Mat scharrInt = new Mat();
        scharr.convertTo(scharrInt, CvType.CV_8U);


        Mat kernel = Mat.ones(new Size(3, 3), CvType.CV_8U);
        Mat dilatedMat = new Mat();
        Imgproc.dilate(scharrInt, dilatedMat, kernel);
        Mat scharrMedian = new Mat();
        Imgproc.medianBlur(dilatedMat, scharrMedian, 3);


        Mat scharrOtsu = new Mat();
        Imgproc.threshold(scharrMedian, scharrOtsu, 0, 255, Imgproc.THRESH_BINARY + Imgproc.THRESH_OTSU);


        Mat morphologyMat = new Mat();
        Imgproc.morphologyEx(scharrOtsu, morphologyMat, Imgproc.MORPH_CLOSE, kernel);


        Core.bitwise_not(morphologyMat, morphologyMat);


        Mat edgeCanny = new Mat();
        Imgproc.Canny(morphologyMat, edgeCanny, 100, 200);


        List<MatOfPoint> contours = new List<MatOfPoint>();
        Imgproc.findContours(edgeCanny, contours, new Mat(), Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

        List<double> areas = contours.ConvertAll(cnt => Imgproc.contourArea(cnt));
        areas.Sort();
        double thresholdArea = areas[(int)(areas.Count * (percentile / 100.0f))];

        List<MatOfPoint> filteredContours = new List<MatOfPoint>();
        foreach (var contour in contours)
        {
            if (Imgproc.contourArea(contour) > thresholdArea)
            {
                filteredContours.Add(contour);
            }
        }

        // Mat result = Mat.zeros(rgbaMat.size(), CvType.CV_8UC3);
        // Imgproc.drawContours(result, filteredContours, -1, new Scalar(255, 255, 255), 1);
        return filteredContours;
    }

    // public void Draw3DPoints(List<Vector3> worldPoints)
    // {
    //     foreach (Vector3 worldPoint in worldPoints)
    //     {
    //         GameObject newPoint = Instantiate(pointPrefab, worldPoint, Quaternion.identity);
    //         newPoint.transform.localScale = Vector3.one * 0.01f;
    //         pointObjects.Add(newPoint);
    //     }
    // }
    // void ClearPreviousPoints()
    // {
    //     foreach (GameObject point in pointObjects)
    //     {
    //         Destroy(point);
    //     }
    //     pointObjects.Clear();
    // }

}




