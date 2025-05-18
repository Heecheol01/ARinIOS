using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class GreenSlopeRT : MonoBehaviour
{
    [Header("AR Components")]
    public ARMeshManager meshManager;
    
    [Header("Plane Visualization")]
    public GameObject planePrefab;
    public Gradient heightGradient;
    public int heightTexSize = 256;

    [Header("Shaders")] 
    public Shader encodeShader;
    public Shader heatShader;

    private Camera bakeCam;
    private RenderTexture heightRT;
    private Material encodeMat;
    private Material rampMat;
    GameObject slopePlane;
    private bool isRunning;

    public void StartSlopeAnalysis(Vector3 pointA, Vector3 pointB)
    {
        if (!isRunning) StartCoroutine(Process(pointA, pointB));
    }

    IEnumerator Process(Vector3 pA, Vector3 pB)
    {
        isRunning = true;
        meshManager.enabled = true;

        if (!bakeCam)
        {
            heightRT = new RenderTexture(heightTexSize, heightTexSize, 16, RenderTextureFormat.RHalf);
            bakeCam = new GameObject("HeightBakeCam").AddComponent<Camera>();
            bakeCam.enabled = false;
            bakeCam.clearFlags = CameraClearFlags.SolidColor;
            bakeCam.backgroundColor = Color.clear;
            bakeCam.orthographic = true;
            bakeCam.targetTexture = heightRT;

            encodeMat = new Material(encodeShader);
        }
        
        Vector3 up = -Physics.gravity.normalized;
        encodeMat.SetVector("_BallPos", new Vector4(pA.x, pA.y, pA.z, 0f));
        encodeMat.SetVector("_Up", new Vector4(up.x, up.y, up.z, 0f));
        
        float len = Vector3.Distance(pA, pB);
        float halfW = 0.25f;
        Vector3 center = (pA + pB) * 0.5f;
        Vector3 forward = (pB - pA).normalized;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > .99f)
            forward = Camera.main.transform.forward.normalized;
            
        Vector3 side = Vector3.Cross(up, forward).normalized;
        Quaternion rot = Quaternion.LookRotation(side, up);
        
        encodeMat.SetVector("_Center", new Vector4(center.x, center.y, center.z, 0));
        encodeMat.SetVector("_Forward", new Vector4(forward.x, forward.y, forward.z, 0));
        encodeMat.SetVector("_Side", new Vector4(side.x, side.y, side.z, 0));
        encodeMat.SetFloat("_HalfW", halfW);
        encodeMat.SetFloat("_Len", len);
        
        slopePlane = Instantiate(planePrefab, center, rot);
        slopePlane.transform.localScale = new Vector3(len * 0.1f, 1, halfW * 0.2f);
            
        bakeCam.transform.SetPositionAndRotation(center + up * 0.1f, Quaternion.LookRotation(-up, forward));
        bakeCam.orthographicSize = len * 0.5f;
        bakeCam.aspect = len / (halfW * 2f);
        bakeCam.nearClipPlane = 0.001f;
        bakeCam.farClipPlane = 5f;
        
        while (meshManager.meshes.Count < 3)
        {
            Debug.Log($"Collecting meshes... Count: {meshManager.meshes.Count}");
            yield return null;
        }
        
        var oldRT = RenderTexture.active;
        RenderTexture.active = heightRT;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = oldRT;
        
        bakeCam.cullingMask = 1 << meshManager.gameObject.layer;
        
        foreach (var mf in meshManager.meshes)
        {
            if (!mf || !mf.sharedMesh)
                continue;
            Graphics.DrawMesh(mf.sharedMesh, mf.transform.localToWorldMatrix, encodeMat, 0, bakeCam);
        }
        bakeCam.Render();

        if (!rampMat)
        {
            rampMat = slopePlane.GetComponent<Renderer>().material;
            rampMat.shader = heatShader;
            rampMat.SetTexture("_RampTex", BuildRampTexture(heightGradient, 256));
        }
        rampMat.SetTexture("_HeightTex", heightRT);
        
        meshManager.enabled = false;
        isRunning = false;
        meshManager.meshes.Clear();
    }

    Texture2D BuildRampTexture(Gradient g, int width)
    {
        var t = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        var c = new Color[width];
        for (int i =0; i < width; i++)
            c[i] = g.Evaluate(i / (float)(width - 1f));
        t.SetPixels(c);
        t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        return t;
    }

    public void ClearMeshes()
    {
        meshManager.meshes.Clear();
        if (slopePlane)
            Destroy(slopePlane);
    }
}
