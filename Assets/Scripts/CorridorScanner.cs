using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARMeshManager))]
public class CorridorScanner : MonoBehaviour
{
    [Header("ARMeshManager (LiDAR)")]
    public ARMeshManager meshManager;

    [Header("Corridor 설정")]
    [Tooltip("공–홀 선분 기준 총 폭 (m)")]
    public float corridorWidth = 1.5f;
    [Tooltip("경사 0°~maxSlope° 매핑")]
    public float maxSlope = 45f;
    [Tooltip("0~1 구간에 매핑할 컬러 그라디언트")]
    public Gradient slopeGradient;

    // 스캔 대상 선분의 양 끝점
    private Vector3 pointA, pointB;
    private bool isScanning = false;

    // TrackableId → 생성된 GameObject 매핑
    private Dictionary<TrackableId, GameObject> corridorObjects = new();

    void Awake()
    {
        if (meshManager == null)
            meshManager = GetComponent<ARMeshManager>();
    }

    /// <summary>
    /// 두 점 A, B를 지정하고 스캔 시작
    /// </summary>
    public void StartScanning(Vector3 A, Vector3 B)
    {
        pointA = A;
        pointB = B;
        isScanning = true;
        meshManager.meshesChanged += OnMeshesChanged;
    }

    /// <summary>
    /// 스캔 종료 및 기존 메시 전부 삭제
    /// </summary>
    public void Clear()
    {
        isScanning = false;
        meshManager.meshesChanged -= OnMeshesChanged;
        foreach (var go in corridorObjects.Values)
            Destroy(go);
        corridorObjects.Clear();
    }

    void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        Debug.Log($"[Scanner] added:{args.added.Count}  updated:{args.updated.Count}  removed:{args.removed.Count}");

        if (!isScanning)
            return;

        foreach (var mf in args.added)   ProcessMesh(mf);
        foreach (var mf in args.updated) ProcessMesh(mf);
        foreach (var mf in args.removed) RemoveMesh(mf);
    }

    void ProcessMesh(MeshFilter mf)
    {
        // 1) TrackableId 파싱
        var id = ExtractTrackableId(mf.name);
        if (id == TrackableId.invalidId)
            return;

        // 2) 분류 정보(Floor/Wall 등) 없으면 스킵
        bool useClassification = false;
        NativeArray<ARMeshClassification> faceCls = default;
#if UNITY_IOS && !UNITY_EDITOR
        if (meshManager.subsystem is XRMeshSubsystem sub)
        {
            faceCls = sub.GetFaceClassifications(id, Allocator.Persistent);
            useClassification = faceCls.IsCreated;
        }
#endif
        if (!useClassification)
            return;

        // 3) 원본 메시 가져오기
        var mesh  = mf.sharedMesh;
        var verts = mesh.vertices;
        var tris  = mesh.triangles;
        var norms = mesh.normals;

        // 4) Corridor 필터링 결과 저장용 리스트
        var worldVerts  = new List<Vector3>();
        var worldTris   = new List<int>();
        var worldColors = new List<Color>();

        // 반폭 계산
        float halfW = corridorWidth * 0.5f;

        // 5) 모든 삼각형 순회
        for (int ti = 0; ti < tris.Length; ti += 3)
        {
            int faceIdx = ti / 3;
            if (faceCls[faceIdx] != ARMeshClassification.Floor)
                continue;

            // 로컬→월드 좌표로 변환
            Vector3 w0 = mf.transform.TransformPoint(verts[tris[ti]]);
            Vector3 w1 = mf.transform.TransformPoint(verts[tris[ti+1]]);
            Vector3 w2 = mf.transform.TransformPoint(verts[tris[ti+2]]);

            // 삼각형 중심점으로만 필터
            Vector3 centroid = (w0 + w1 + w2) / 3f;
            if (!InsideCorridor(centroid, halfW))
                continue;

            // 경사 계산 (법선→월드 방향)
            Vector3 n0 = mf.transform.TransformDirection(norms[tris[ti]]);
            float slope = Vector3.Angle(n0, Vector3.up);
            float t     = Mathf.Clamp01(slope / maxSlope);
            Color c     = (slopeGradient != null) ? slopeGradient.Evaluate(t) : Color.white;

            // 결과 버텍스/컬러/인덱스 추가
            int baseIdx = worldVerts.Count;
            worldVerts.AddRange(new[]{ w0, w1, w2 });
            worldColors.AddRange(new[]{ c, c, c });
            worldTris.AddRange(new[]{ baseIdx, baseIdx+1, baseIdx+2 });
        }

#if UNITY_IOS && !UNITY_EDITOR
        faceCls.Dispose();
#endif

        // 6) 결과가 있으면 생성·업데이트, 없으면 제거
        if (worldVerts.Count > 0)
        {
            if (!corridorObjects.TryGetValue(id, out var go))
            {
                go = new GameObject($"Corridor_{id}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                // Vertex Color 지원 Unlit 셰이더 사용 권장
                mr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                corridorObjects[id] = go;
            }
            UpdateMesh(corridorObjects[id], worldVerts, worldTris, worldColors);
        }
        else if (corridorObjects.TryGetValue(id, out var oldGo))
        {
            Destroy(oldGo);
            corridorObjects.Remove(id);
        }
    }

    void RemoveMesh(MeshFilter mf)
    {
        var id = ExtractTrackableId(mf.name);
        if (corridorObjects.TryGetValue(id, out var go))
        {
            Destroy(go);
            corridorObjects.Remove(id);
        }
    }

    void UpdateMesh(GameObject go, List<Vector3> v, List<int> t, List<Color> c)
    {
        var mf = go.GetComponent<MeshFilter>();
        var m  = mf.mesh ?? new Mesh();
        m.Clear();
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.SetColors(c);
        m.RecalculateNormals();
        mf.mesh = m;
    }

    bool InsideCorridor(Vector3 p, float halfWidth)
    {
        Vector3 AB = pointB - pointA;
        float  tt = Mathf.Clamp01(Vector3.Dot(p - pointA, AB) / AB.sqrMagnitude);
        Vector3 proj = pointA + tt * AB;
        return (p - proj).sqrMagnitude <= halfWidth * halfWidth;
    }

    TrackableId ExtractTrackableId(string name)
    {
        // "MeshFilter 1234-abcdef" 형태에서 ID 부분만 파싱
        var parts = name.Split(' ');
        if (parts.Length < 2) return TrackableId.invalidId;
        var toks = parts[1].Split('-');
        if (toks.Length != 2) return TrackableId.invalidId;
        if (ulong.TryParse(toks[0], out var s1) && ulong.TryParse(toks[1], out var s2))
            return new TrackableId(s1, s2);
        return TrackableId.invalidId;
    }
}
