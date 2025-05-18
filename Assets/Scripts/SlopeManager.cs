using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class SlopeManager : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    // public GreenSlopePipeline greenSlope;
    public GreenSlopeRT greenSlope;
    
    static List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    public Vector2 centerVec;
    public Transform pivot;
    public GameObject prefab;

    private List<GameObject> _points = new List<GameObject>();
    private Vector3 _startPos;
    private Vector3 _endPos;
    private bool _rulerEnable;
    private Vector3 _rulerPosSave;
    private Pose _poseSave;

    private int _count = 0;
    // Start is called before the first frame update
    void Start()
    {
        centerVec = new Vector2( Screen.width * 0.5f, Screen.height * 0.5f);
    }

    // Update is called once per frame
    void Update()
    {
        if(raycastManager.Raycast(centerVec, _hits, TrackableType.PlaneWithinPolygon))
        {
            _poseSave = _hits[0].pose; // 첫번째로 측정된 면의 정보를 가져옴.
            float hitDis = _hits[0].distance;
            if(hitDis < 0.1f) hitDis = 0.1f;
            if(hitDis > 0.5f) hitDis = 0.5f;
            hitDis = hitDis * -0.25f + 1.45f;
            
            _rulerPosSave = _poseSave.position;
            pivot.localScale = new Vector3(hitDis,hitDis,hitDis);
            pivot.position = Vector3.Lerp(pivot.position, _poseSave.position,0.2f);
            pivot.rotation = Quaternion.Lerp(pivot.rotation,_poseSave.rotation,0.2f);
        }
        else
        {
            Quaternion tRot = Quaternion.Euler(90f,0,0);
            pivot.localScale = new Vector3(0.5f,0.5f,0.5f);
            pivot.rotation = Quaternion.Lerp(pivot.rotation,tRot,0.5f);
            pivot.localPosition = Vector3.Lerp(pivot.localPosition, Vector3.zero,0.5f);
        }
    }
    
    public void OnConfirmLine()
    {
        Vector3 a = _startPos;
        Vector3 b = _endPos;

        Debug.Log($"▶ RulerManager ▸ StartSlopeAnalysis A:{a:F3}  B:{b:F3}");
        greenSlope.StartSlopeAnalysis(a, b);
    }

    public void MakeRulerObj()
    {
        if (_count % 2 == 0)
        {
            // greenSlope.ClearMeshes();
            MakeStartAnchor();
        }
        else if (_count % 2 == 1)
        {
            MakeEndAnchor();
            OnConfirmLine();
        }
        _count++;
    }

    private void MakeStartAnchor()
    {
        _startPos = _rulerPosSave;
        _points.Add(Instantiate(prefab, _startPos, pivot.rotation));
    }

    private void MakeEndAnchor()
    {
       _endPos = _rulerPosSave;
       _points.Add(Instantiate(prefab, _endPos, pivot.rotation));
    }
}