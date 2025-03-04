using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ImgcodecsModule;
using OpenCVForUnity.TextModule;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Primitives;
public class WtoS : MonoBehaviour
{
    public Camera _camera;
    private readonly int frameCount = 64;
    private readonly string path = "Debug/data1";
    public GameObject panel;
    private Texture2D texture;
    private Vector3[] screenPoints;
    private Mat rawScreenMat;

    void Start()
    {
        Vector3[] depthPoints = LoadDepthPoints(Path.Combine(path, $"DepthPoints/{frameCount}"));
        XRCameraIntrinsics cameraIntrinsics = LoadCameraIntrinsics(Path.Combine(path, $"CameraIntrinsics/{frameCount}"));
        rawScreenMat = LoadImageMat(Path.Combine(Path.Combine($"{Application.dataPath}/Resources", path), $"RawScreen/{frameCount}.png"));
        texture = new Texture2D(rawScreenMat.width(), rawScreenMat.height());

        if (LoadCameraTransform(Path.Combine(path, $"CameraTransform/{frameCount}"), out Vector3 position, out Quaternion rotation))
        {
            _camera.transform.SetPositionAndRotation(position, rotation);

            List<Vector3> pointsList = new List<Vector3>();
            
            foreach (Vector3 point in depthPoints)
            {
                Vector3 screenPoints = _camera.WorldToScreenPoint(point);
                pointsList.Add(screenPoints);
                Render2DPoint(screenPoints);
            }
            screenPoints = pointsList.ToArray();
            Debug.Log($"{screenPoints.Length}");

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach(Vector3 point in screenPoints)
            {
                if(point.x>maxX)
                {
                    maxX=point.x;
                }
                if(point.y>maxY)
                {
                    maxY=point.y;
                }
                if(point.x<minX)
                {
                    minX=point.x;
                }
                if(point.y<minY)
                {
                    minY=point.y;
                }
            }
            Debug.Log($"{minX} {minY} {maxX} {maxY}");
            Utils.matToTexture2D(rawScreenMat, texture);
            
            // SaveScreenPoints(screenPoints,Path.Combine(path, $"ScreenPoints/{frameCount}.txt"));
            SetScreen();
        }


        void Update()
        {

        }
    }

    // public Vector2[] FindCorner(Vector3[] points)
    // {
    //     if (points == null || points.Length == 0)
    //     {
    //         Debug.LogError("null");
    //         return new Vector2[4];
    //     }
    //     Vector2[] corners = new Vector2[4];

    //     float minX = float.MaxValue, maxX = float.MinValue;
    //     float minY = float.MaxValue, maxY = float.MinValue;


    //     foreach (Vector2 point in points)
    //     {
    //         if (point.x <= minX && point.y >= maxY)
    //         {
    //             minX = point.x;
    //             maxY = point.y;
    //             corners[0] = new Vector2(point.x,point.y);
    //         }
    //         if (point.x >= maxX && point.y >= maxY)
    //         {
    //             maxX = point.x;
    //             maxY = point.y;
    //             corners[1] = new Vector2(point.x,point.y);
    //         }
    //         if (point.x <= minX && point.y <= minY)
    //         {
    //             minX = point.x;
    //             minY = point.y;
    //             corners[2] = new Vector2(point.x,point.y);
    //         }
    //         if (point.x >= maxX && point.y <= minY)
    //         {
    //             maxX = point.x;
    //             minY = point.y;
    //             corners[3] = new Vector2(point.x,point.y);
    //         }
    //     }

    //     return corners;
    // }

    private void MakeDirectoryIfNotExists(string dirName, bool isDelete = true)
        {
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            else
            {
                if (!isDelete) return;
                Directory.Delete(dirName, true);
                Directory.CreateDirectory(dirName);
            }
        }

    public Mat GetTilesMask(Mat imageMat)
    {
        Vector2[] a = new Vector2[0];
        if (imageMat == null || imageMat.empty())
            return null;


        Mat grayMat = new Mat();
        Imgproc.cvtColor(imageMat, grayMat, Imgproc.COLOR_RGBA2GRAY);


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
        return morphologyMat;
    }
    public void Render2DPoint(Vector3 center, int radius = 2, Scalar color = default)
    {
        float depth = Mathf.Clamp01(center.z);


        if (color == default)
        {
            color = new Scalar(255 * depth, 0 * depth, 0 * depth);
        }

        Imgproc.circle(rawScreenMat, new Point((int)center.x, (int)center.y), radius, color, -1);

    }

    public void SaveScreenPoints(Vector3[] points,string savePath)
    {
        using (StreamWriter writer = new StreamWriter(savePath))
        {
            foreach (Vector3 point in points)
            {
                writer.WriteLine($"{point.x},{point.y},{point.z}");
            }
        }
    }

    public Vector3[] LoadDepthPoints(string filePath)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(filePath);

        if (textAsset == null)
        {
            Debug.Log($"{filePath}");
            Debug.LogError("DepthPoint file not found");
            return null;
        }
        string[] lines = textAsset.text.Split('\n');
        Vector3[] depthPoints = new Vector3[lines.Length - 1];
        for (int i = 0; i < lines.Length - 1; i++)
        {
            string[] pos = lines[i].Split(',');
            depthPoints[i] = new Vector3(float.Parse(pos[0]), float.Parse(pos[1]), float.Parse(pos[2]));
        }
        return depthPoints;
    }

    public float[] LoadDepth(string filePath)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(filePath);

        if (textAsset == null)
        {
            Debug.LogError("Depth file not found");
            return null;
        }
        string[] lines = textAsset.text.Split('\n');
        float[] depth = new float[lines.Length - 1];
        for (int i = 0; i < lines.Length - 1; i++)
        {
            depth[i] = float.Parse(lines[i]);
        }
        return depth;
    }

    public XRCameraIntrinsics LoadCameraIntrinsics(string filePath)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(filePath);
        if (textAsset == null)
        {
            Debug.LogError("CameraIntrinsics file not found");
            return default;
        }
        string[] lines = textAsset.text.Split('\n');

        float fx = float.Parse(lines[0]);
        float fy = float.Parse(lines[1]);
        float cx = float.Parse(lines[2]);
        float cy = float.Parse(lines[3]);
        int Resolutionx = int.Parse(lines[4]);
        int Resolutiony = int.Parse(lines[5]);
        return new XRCameraIntrinsics(new Vector2(fx, fy), new Vector2(cx, cy), new Vector2Int(Resolutionx, Resolutiony));
    }

    public Mat LoadImageMat(string filePath)
    {
        Mat mat = Imgcodecs.imread(filePath);
        return mat;
    }

    public void SetScreen()
    {
        // byte[] imageBytes = File.ReadAllBytes(imagePath);
        // Texture2D texture = new Texture2D(0, 0);
        // texture.LoadImage(imageBytes);
        UnityEngine.UI.Image panelImage = panel.GetComponent<UnityEngine.UI.Image>();
        Sprite sprite = Sprite.Create(texture, new UnityEngine.Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
        panelImage.sprite = sprite;
    }

    public bool LoadCameraTransform(string filePath, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        TextAsset textAsset = Resources.Load<TextAsset>(filePath);
        if (textAsset == null)
        {
            Debug.LogWarning("CameraTransform file not found: ");
            return false;
        }

        string[] lines = textAsset.text.Split('\n');
        string[] positions = lines[0].Split(',');

        float x = float.Parse(positions[0]);
        float y = float.Parse(positions[1]);
        float z = float.Parse(positions[2]);
        position = new Vector3(x, y, z);

        string[] rotations = lines[1].Split(',');

        float qx = float.Parse(rotations[0]);
        float qy = float.Parse(rotations[1]);
        float qz = float.Parse(rotations[2]);
        float qw = float.Parse(rotations[3]);
        rotation = new Quaternion(qx, qy, qz, qw);

        return true;
    }




}
