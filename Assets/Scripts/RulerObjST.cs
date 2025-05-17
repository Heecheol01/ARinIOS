using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RulerObjST : MonoBehaviour
{
    public Vector3 startPos;
    public Vector3 endPos;   
    public LineRenderer lineObj;
    public Transform textObj;
    public TextMesh text;
    public Transform mainCam;

    public void SetInit(Vector3 pos)
    {
        startPos = pos;
        lineObj.SetPosition(0,pos);
    }

    public void SetObj(Vector3 pos)
    {
        endPos = pos;
        lineObj.SetPosition(1,pos);
    }

    void Update()
    {
        Vector3 tVec = endPos - startPos;
        textObj.position = startPos + tVec * 0.5f;

        float tDis = tVec.magnitude;
        string tDisText = $"{tDis:N2}m";
        text.text = tDisText;

        textObj.LookAt(mainCam);
    }
}