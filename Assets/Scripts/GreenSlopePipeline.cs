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

    /*------------------------------  PUBLIC API  ------------------------------*/
    /// <summary>RulerManager가 두 점을 확정하면 이 메서드만 호출해 주세요.</summary>
    public void StartSlopeAnalysis(Vector3 pointA, Vector3 pointB, Pose pose)
    {
        if (!isRunning) StartCoroutine(ProcessPipeline(pointA, pointB, pose));
    }

    /*------------------------------  MAIN  ------------------------------*/
    private IEnumerator ProcessPipeline(Vector3 pA, Vector3 pB, Pose pose)
    {
        isRunning = true;
        Debug.Log($"ProcessPipeline START - A:{pA:F3}  B:{pB:F3}");

        /* 0) LiDAR 스캔 ON ---------------------------------------------------*/
        meshManager.enabled = true;
        Debug.Log("ARMeshManager enabled, waiting for meshes…");

        // meshes가 충분히 쌓일 때까지 최대 3초 대기
        float tStart = Time.time;
        while (Time.time - tStart < 3f && meshManager.meshes.Count < 30)
        {
            yield return null;
        }
        Debug.Log($"Mesh ready - Count:{meshManager.meshes.Count}");

        /* 1) ROI 설정 --------------------------------------------------------*/
        float length    = Vector3.Distance(pA, pB);
        float halfWidth = 0.25f;                       // 좌 · 우 0.25 m
        Vector3 center  = (pA + pB) * 0.5f;
        Vector3 forward = (pB - pA).normalized;
        Vector3 up      = -Physics.gravity.normalized;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
            up = Camera.main.transform.forward.normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        Debug.Log($"ROI center:{center:F3}  len:{length:F2}  halfW:{halfWidth}");
        
        Quaternion orient = Quaternion.Lerp(planePrefab.transform.rotation, pose.rotation,0.2f);
        
        slopePlane = Instantiate(planePrefab, center, orient);

        /* 2) ROI 내부 버텍스 수집 -------------------------------------------*/
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
                if (Mathf.Abs(lv.x) <= halfWidth &&                  // 좌우 폭
                    lv.z >= -length * 0.5f && lv.z <=  length * 0.5f) // 앞뒤 길이
                {
                    roiVerts.Add(wv);
                }
            }
        }
        Debug.Log($"ROI vertex count = {roiVerts.Count}");

        if (roiVerts.Count < 10)
        {
            Debug.LogWarning("ROI에 충분한 버텍스가 없습니다 – 분석 취소.");
            meshManager.enabled = false;
            isRunning = false;
            yield break;
        }

        // /* 3) 평균 법선(최소제곱 평면) ----------------------------------------*/
        // Vector3 mean = roiVerts.Aggregate(Vector3.zero, (sum, v) => sum + v) / roiVerts.Count;
        // float xx=0, yy=0, zz=0, xy=0, xz=0, yz=0;
        // foreach (var v in roiVerts)
        // {
        //     Vector3 d = v - mean;
        //     xx += d.x*d.x;  yy += d.y*d.y;  zz += d.z*d.z;
        //     xy += d.x*d.y;  xz += d.x*d.z;  yz += d.y*d.z;
        // }
        // Matrix4x4 cov = new Matrix4x4(
        //     new Vector4(xx, xy, xz, 0),
        //     new Vector4(xy, yy, yz, 0),
        //     new Vector4(xz, yz, zz, 0),
        //     new Vector4( 0,  0,  0, 1)
        // );
        // Vector3 nGround = SmallestEigenVector(cov).normalized;
        // Debug.Log($"Ground normal = {nGround:F3}");
        //
        // /* 4) Plane 배치 ------------------------------------------------------*/
        // Vector3 fProj = Vector3.ProjectOnPlane(forward, nGround).normalized;
        // if (fProj.sqrMagnitude < 1e-4f)
        //     fProj = Vector3.Cross(nGround, Vector3.right).normalized;
        // Quaternion orient = Quaternion.LookRotation(fProj, nGround);
        //
        // if (slopePlane) Destroy(slopePlane);
        // slopePlane = Instantiate(planePrefab, center, orient);
        // float unit = slopePlane.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
        // slopePlane.transform.localScale = new Vector3(length / unit, 1, halfWidth * 2f);
        //
        // Debug.Log($"Plane instantiated - pos:{center:F3}  rot:{orient.eulerAngles:F1}");
        //
        // /* 5) 그라디언트 컬러 적용 -------------------------------------------*/
        // var meshFilter = slopePlane.GetComponent<MeshFilter>();
        // var pMesh      = meshFilter.mesh;
        // var pv         = pMesh.vertices;
        // var cols       = new Color[pv.Length];
        // float minY = pv.Min(v => v.y);
        // float maxY = pv.Max(v => v.y);
        // for (int i = 0; i < pv.Length; i++)
        //     cols[i] = heightGradient.Evaluate(Mathf.InverseLerp(minY, maxY, pv[i].y));
        // pMesh.colors = cols;
        // Debug.Log("Vertex colors (gradient) applied.");

        /* 6) 정리 ------------------------------------------------------------*/
        meshManager.enabled = false;
        isRunning = false;
        Debug.Log("ProcessPipeline END\n");
    }

    /*------------------------------  UTIL  ------------------------------*/
    Vector3 SmallestEigenVector(Matrix4x4 m)
    {
        Matrix4x4 inv = m.inverse;
        Vector3 v = Vector3.up;
        for (int i = 0; i < 10; i++)
            v = inv.MultiplyVector(v).normalized;
        return v;
    }
}
