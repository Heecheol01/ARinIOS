using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class GreenSlopePipeline : MonoBehaviour
{
    [Header("AR Components")]
    public ARRaycastManager raycastManager;
    public ARMeshManager    meshManager;

    [Header("Plane Visualization")]
    public GameObject planePrefab;
    public Gradient  heightGradient;

    private bool          isRunning;
    private GameObject    slopePlane;
    
    public void StartSlopeAnalysis(Vector3 pointA, Vector3 pointB)
    {
        if (!isRunning) StartCoroutine(ProcessPipeline(pointA, pointB));
    }
    
    private IEnumerator ProcessPipeline(Vector3 pA, Vector3 pB)
    {
        isRunning = true;
        meshManager.enabled = true;
        Debug.Log("ProcessPipeline Start");

        float tStart = Time.time;
        while (Time.time - tStart < 5f && meshManager.meshes.Count < 30)
        {
            yield return null;
        }
        Debug.Log($"Mesh ready - Count:{meshManager.meshes.Count}");

        float length    = Vector3.Distance(pA, pB);
        float halfWidth = 0.25f;
        Vector3 center  = (pA + pB) * 0.5f;
        Vector3 forward = (pB - pA).normalized;
        Vector3 up      = -Physics.gravity.normalized;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
            up = Camera.main.transform.forward.normalized;
        
        PlaceSlopePlane(pA, pB);
        
        var roiVerts = new List<Vector3>();
        Matrix4x4 toWorld = Matrix4x4.TRS(center, Quaternion.LookRotation(forward, up), Vector3.one);
        Matrix4x4 inv     = toWorld.inverse;

        foreach (var mf in meshManager.meshes)
        {
            var mesh = mf.sharedMesh;
            if (!mesh) continue;

            Matrix4x4 trs = mf.transform.localToWorldMatrix;
            foreach (var v in mesh.vertices)
            {
                Vector3 wv = trs.MultiplyPoint3x4(v);
                Vector3 lv = inv.MultiplyPoint3x4(wv);
                if (Mathf.Abs(lv.x) <= halfWidth &&
                    lv.z >= -length * 0.5f && lv.z <=  length * 0.5f)
                {
                    roiVerts.Add(wv);
                }
            }
        }
        
        Debug.Log($"ROI Vertices Count : {roiVerts.Count}");
        
        if (roiVerts.Count < 30)
        {
            Debug.Log("ROI에 충분한 버텍스가 없습니다 – 1차 프리뷰");
        }
        
        Texture2D heightTex = BuildHeightTexture(roiVerts, slopePlane, 128);
        Texture2D rampTex = BuildRampTexture(heightGradient, 256);
        
        Material mat = slopePlane.GetComponent<Renderer>().material;
        mat.SetTexture("_HeightTex", heightTex);
        mat.SetTexture("_RampTex",  rampTex);
        
        meshManager.enabled = false;
        isRunning = false;
        Debug.Log("ProcessPipeline END");
    }
    
    void PlaceSlopePlane(Vector3 pA, Vector3 pB)
    {
        float length = Vector3.Distance(pA, pB);
        float halfWidth = 0.25f;
        Vector3 center = (pA + pB) * 0.5f;
        Vector3 forward = (pB - pA).normalized;
        Vector3 up      = -Physics.gravity.normalized;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
            up = Camera.main.transform.forward.normalized;
        
        Vector3 zAxis = Vector3.Cross(up, forward).normalized;
        Quaternion orient = Quaternion.LookRotation(zAxis, up);
        
        slopePlane = Instantiate(planePrefab, center, orient);
        slopePlane.transform.localScale = new Vector3(length * 0.1f, 1f, halfWidth * 0.2f);
    }

    Texture2D BuildHeightTexture(List<Vector3> roi, GameObject plane, int texSize)
    {
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        
        var worldToPlane = plane.transform.worldToLocalMatrix;
        float[,] hBuffer = new float[texSize, texSize];
        bool[,] has = new bool[texSize, texSize];

        float meanH = roi.Average(v => worldToPlane.MultiplyPoint3x4(v).y);
        float maxAbs = 1e-4f;

        int count = 0;
        
        foreach (var wv in roi)
        {
            Vector3 lv = worldToPlane.MultiplyPoint3x4(wv);
            int x = Mathf.Clamp(Mathf.RoundToInt((lv.x + .5f) * (texSize-1)), 0, texSize - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt((lv.z + .5f) * (texSize-1)), 0, texSize - 1);
            
            float dy = lv.y - meanH;
            hBuffer[x, y] = dy;
            has[x, y] = true;
            maxAbs = Mathf.Max(maxAbs, Math.Abs(dy));
        }
        
        Color[] pixels = new Color[texSize * texSize];
        for (int j = 0; j < texSize; j++)
        for (int i = 0; i < texSize; i++)
        {
            if (!has[i, j]) { pixels[j*texSize+i] = new Color(0, 0, 0, 0); count++; continue; }
            
            float t = hBuffer[j, i] / (2f * maxAbs) + 0.5f;
            pixels[j * texSize + i] = new Color(t, 0, 0, 1f);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        
        Debug.Log($"텍스쳐를 가지고 있는 버퍼의 수 : {has.Length - count}");
        
        return tex;
    }

    Texture2D BuildRampTexture(Gradient gradient, int texSize)
    {
        Texture2D ramp =  new Texture2D(texSize, 1, TextureFormat.RGBA32, false);
        var cols = new Color[texSize];
        for (int i = 0; i < texSize; i++)
            cols[i] = gradient.Evaluate(i / (texSize - 1f));
        ramp.SetPixels(cols);
        ramp.Apply();
        return ramp;
    }
    
    public void DestroyPlane()
    {
        if (slopePlane)
            Destroy(slopePlane);
    }
}
