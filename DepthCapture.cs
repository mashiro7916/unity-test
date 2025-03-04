using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;


public class DepthCapture : MonoBehaviour
{
    public AROcclusionManager occlusionManager;
    public ARCameraManager cameraManager;
    public Camera _camera;
    private Vector3[] points;
    private Vector3[] screenPoints;

    void Start()
    {
        occlusionManager = GetComponent<AROcclusionManager>();
        if (occlusionManager == null)
        {
            Debug.Log("occlusion null");
            return;
        }
        if (cameraManager == null)
        {
            Debug.Log("camera null");
            return;
        }
    }

    void Update()
    {
        Clear3DPoints();

        if (occlusionManager.TryAcquireEnvironmentDepthCpuImage(out var depthImage))
        {

            using (depthImage)
            {
                points = new Vector3[depthImage.width * depthImage.height];
                if (cameraManager.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
                {
                    NativeArray<float> depths = GetDepth(depthImage.GetPlane(0));
                    points = TransformDepthPointsToWorld(depthImage.width, depthImage.height, depths, cameraIntrinsics);
                }
                if (points.Length > 0)
                {
                    List<Vector3> pointsList = new List<Vector3>();

                    foreach (Vector3 point in points)
                    {
                        Vector3 screenPoints = _camera.WorldToScreenPoint(point);
                        pointsList.Add(screenPoints);
                    }
                    screenPoints = pointsList.ToArray();
                    Debug.Log($"{screenPoints.Length}");
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    foreach (Vector3 point in screenPoints)
                    {
                        if (point.x > maxX)
                        {
                            maxX = point.x;
                        }
                        if (point.y > maxY)
                        {
                            maxY = point.y;
                        }
                        if (point.x < minX)
                        {
                            minX = point.x;
                        }
                        if (point.y < minY)
                        {
                            minY = point.y;
                        }
                    }
                    Debug.Log($"{minX} {minY} {maxX} {maxY}");
                }

            }
            depthImage.Dispose();
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

    public void Clear3DPoints()
    {
        foreach (GameObject point in GameObject.FindGameObjectsWithTag("Cad3DPoint"))
        {
            Destroy(point.GetComponent<Renderer>().material);
            Destroy(point);
        }
    }
    private NativeArray<float> GetDepth(XRCpuImage.Plane plane)
    {
        return plane.data.Reinterpret<float>(plane.pixelStride);
    }

    private float[,] CalculateIntrinsicsForDepthCamera(
            XRCameraIntrinsics cameraIntrinsics,
            int depthWidth,
            int depthHeight
        )
    {
        int rgbCameraWidth = cameraIntrinsics.resolution.x;
        int rgbCameraHeight = cameraIntrinsics.resolution.y;

        float scaleX = (float)rgbCameraWidth / depthWidth;
        float scaleY = (float)rgbCameraHeight / depthHeight;

        float[,] intrinsicsForDepth = new float[3, 3];

        intrinsicsForDepth[0, 0] = cameraIntrinsics.focalLength.x / scaleX;
        intrinsicsForDepth[0, 1] = 0;
        intrinsicsForDepth[0, 2] = cameraIntrinsics.principalPoint.x / scaleX;

        intrinsicsForDepth[1, 0] = 0;
        intrinsicsForDepth[1, 1] = cameraIntrinsics.focalLength.y / scaleY;
        intrinsicsForDepth[1, 2] = cameraIntrinsics.principalPoint.y / scaleY;

        intrinsicsForDepth[2, 0] = 0;
        intrinsicsForDepth[2, 1] = 0;
        intrinsicsForDepth[2, 2] = 1;

        return intrinsicsForDepth;
    }
    public Vector3[] TransformDepthPointsToWorld(
            int depthWidth,
            int depthHeight,
            NativeArray<float> depths,
            XRCameraIntrinsics cameraIntrinsics
        )
    {
        float[,] intrinsicsForDepth = CalculateIntrinsicsForDepthCamera(
            cameraIntrinsics,
            depthWidth,
            depthHeight
        );
        Vector3[] result = new Vector3[depthWidth * depthHeight];
        for (int y = 0; y < depthHeight; y++)
        {
            for (int x = 0; x < depthWidth; x++)
            {
                int flippedY = depthHeight - 1 - y;
                Vector3 depthPointInWorldCoordinate = TransformDepthPointToWorld(
                    x,
                    flippedY,
                    depths[y * depthWidth + x],
                    intrinsicsForDepth
                );
                result[y * depthWidth + x] = depthPointInWorldCoordinate;
            }
        }
        depths.Dispose();
        return result;
    }

    private Vector3 TransformDepthPointToWorld(
            int x,
            int y,
            float depth,
            float[,] intrinsicsForDepth
        )
    {
        float principalPointX = intrinsicsForDepth[0, 2];
        float principalPointY = intrinsicsForDepth[1, 2];

        float focalLengthX = intrinsicsForDepth[0, 0];
        float focalLengthY = intrinsicsForDepth[1, 1];

        float xrw = (x - principalPointX) * depth / focalLengthX;
        float yrw = (y - principalPointY) * depth / focalLengthY;
        float zrw = depth;

        Vector3 result = new(xrw, yrw, zrw);
        return _camera.transform.TransformPoint(result);
    }

    public Vector3[] FindCorner(Vector3[] pointCloud)
    {
        if (pointCloud == null || pointCloud.Length == 0)
        {
            Debug.LogError("null");
            return new Vector3[4];
        }
        Vector3[] corner = new Vector3[4];

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (Vector3 worldPoint in pointCloud)
        {

            Vector3 screenPoint = _camera.WorldToScreenPoint(worldPoint);
            float screenX = screenPoint.x;
            float screenY = screenPoint.y;
            if (screenX <= minX && screenY >= maxY)
            {
                minX = screenX;
                maxY = screenY;
                corner[0] = worldPoint;
            }
            if (screenX >= maxX && screenY >= maxY)
            {
                maxX = screenX;
                maxY = screenY;
                corner[1] = worldPoint;
            }
            if (screenX <= minX && screenY <= minY)
            {
                minX = screenX;
                minY = screenY;
                corner[2] = worldPoint;
            }
            if (screenX >= maxX && screenY <= minY)
            {
                maxX = screenX;
                minY = screenY;
                corner[3] = worldPoint;
            }
        }

        return corner;
    }


}
