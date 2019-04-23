using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// Inspired from https://github.com/OneLoneCoder/olcPixelGameEngine/blob/master/OneLoneCoder_PGE_ShadowCasting2D.cpp
/// 
/// needs heavy refactoring
/// </summary>
public class VisionScript : MonoBehaviour
{
    //Transform transform;
    [SerializeField]
    float speed = 10;
    public float radius = 5f;

    public LayerMask mask;

    public Material mat;
    MeshRenderer renderer;
    MeshCollider meshCollider;


    Segment[] Segments;
    Vector2[] AllPoints;
    MeshFilter MeshFilter;
    List<PointAndAngle> PointAndAngles = new List<PointAndAngle>();

    int NumberOfCalculations = 0;
    double Sum = 0;
    // Start is called before the first frame update
    void Start()
    {
        //transform = GetComponent<Transform>();
        meshCollider = GetComponent<MeshCollider>();
        Segments = FindAllLines();
        //TODO: Optimize this;
        var points = new List<Vector2>();
        foreach (var item in Segments)
        {
            if (!points.Contains(item.a))
            {
                points.Add(item.a);
            }
            if (!points.Contains(item.b))
            {
                points.Add(item.b);
            }
        }
        AllPoints = points.ToArray();
    }
    private void Awake()
    {
        MeshFilter = GetComponentInChildren<MeshFilter>();
        renderer = GetComponent<MeshRenderer>();
        renderer.enabled = true;
        renderer.material = mat;
        renderer.material.renderQueue = 1800;
    }

    //private void OnDrawGizmos()
    //{
    //    foreach (var item in PointAndAngles)
    //    {
    //        Gizmos.DrawSphere(item.Point, 0.2f);
    //    }
    //}
    // Update is called once per frame
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<float> angles = new List<float>(); //TODO: FIND BETTER;
    void Update()
    {
        var cam = Camera.main;
        var vert = 2*cam.orthographicSize;//Camera.main.orthographicSize;
        var horz =  (vert * Screen.width / Screen.height);
        var camPos = Camera.main.transform.position;
        var northeast = new Vector3(camPos.x + vert, camPos.y + horz, 0);
        var southeast = new Vector3(camPos.x + vert, camPos.y - horz, 0);
        var southwest = new Vector3(camPos.x - vert, camPos.y - horz, 0);
        var northwest = new Vector3(camPos.x - vert, camPos.y + horz, 0);
        AllPoints[AllPoints.Length - 4] = northeast;
        AllPoints[AllPoints.Length - 3] = southeast;
        AllPoints[AllPoints.Length - 2] = southwest;
        AllPoints[AllPoints.Length - 1] = northwest;
        Segments[Segments.Length - 4] = new Segment { a = northeast, b = southeast };
        Segments[Segments.Length - 3] = new Segment { a = southeast, b = southwest };
        Segments[Segments.Length - 2] = new Segment { a = southwest, b = northwest };
        Segments[Segments.Length - 1] = new Segment { a = northwest, b = northeast };

        var Watch = System.Diagnostics.Stopwatch.StartNew();
        NumberOfCalculations++;

        angles.Clear();
        for (int i = 0; i < AllPoints.Length; i++)
        {
            var delta = (Vector3)AllPoints[i] - transform.position;
            float angle = Mathf.Atan2(delta.y, delta.x);
            //Debug.DrawRay(transform.position, delta, Color.white);
            angles.Add(angle - 0.001f);
            angles.Add(angle);
            angles.Add(angle + 0.001f);
        }

        PointAndAngles.Clear();

        PointAndAngles = FinderIntersections(angles, transform.position);

        //MESH GENERATION
        vertices.Clear();
        triangles.Clear();

        vertices.Add(transform.InverseTransformPoint(transform.position)); // Wonder if conversion is needed

        for (int i = 0; i < PointAndAngles.Count-1; i++)
        {
            var point = PointAndAngles[i];
            var point2 = PointAndAngles[i + 1];
            vertices.Add(transform.InverseTransformPoint(point.x, point.y,transform.position.z));
            vertices.Add(transform.InverseTransformPoint(point2.x, point2.y,transform.position.z));
        }
        vertices.Add(transform.InverseTransformPoint(PointAndAngles[PointAndAngles.Count-1].x, PointAndAngles[PointAndAngles.Count -1].y,0));
        vertices.Add(transform.InverseTransformPoint(PointAndAngles[0].x, PointAndAngles[0].y,0));

        for (int i = 0; i < vertices.Count-1; i++)
        {
            triangles.Add((i + 1));
            triangles.Add((i));
            triangles.Add(0);
        }


        MeshFilter.mesh.Clear();
        var mesh = new Mesh()
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),

        };
        //mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        //mesh.RecalculateTangents();

        //meshCollider.sharedMesh = mesh;
        MeshFilter.mesh = mesh;

        Watch.Stop();
        Sum += Watch.Elapsed.TotalMilliseconds;
        //Debug.Log($"ELAPSED: {Watch.Elapsed}, Average: {Sum / NumberOfCalculations}");
    }

    private List<PointAndAngle> FinderIntersections(List<float> angles, Vector3 origPos)
    {
        var pointAndAngles = new List<PointAndAngle>();
        for (int i = 0; i < angles.Count; i++)
        {
            var raydeltax = radius * Mathf.Cos(angles[i]);
            var raydeltay = radius * Mathf.Sin(angles[i]);
            var min_t1 = float.MaxValue;
            float minIntersectx = 0;
            float minIntersecty = 0;
            var min_angle = 0f;
            var found = false;
            //Debug.DrawRay(transform.position, new Vector3(raydeltax, raydeltay), Color.yellow);
            for (int j = 0; j < Segments.Length; j++)
            {
                var segmentDelta = Segments[j].b - Segments[j].a;
                if (Mathf.Abs(segmentDelta.x - raydeltax) > 0 && Mathf.Abs(segmentDelta.y - raydeltay) > 0)
                {
                    var seg = Segments[j];
                    var t2 = (raydeltax * (seg.a.y - origPos.y) + (raydeltay * (origPos.x - seg.a.x))) / (segmentDelta.x * raydeltay - segmentDelta.y * raydeltax);
                    var t1 = (seg.a.x + segmentDelta.x * t2 - origPos.x) / raydeltax;
                    if (t1 > 0 && t2 >= 0 && t2 <= 1.0f)
                    {
                        if (t1 < min_t1)
                        {
                            min_t1 = t1;
                            //TODO: Maybe not new em up here... Potential GC overload??
                            minIntersectx = origPos.x + raydeltax * t1;
                            minIntersecty = origPos.y + raydeltay * t1;

                            min_angle = PseudoAngle(minIntersectx - origPos.x,minIntersecty - origPos.y);
                            found = true;
                        }
                    }
                }
            }
            if (found)
            {
                pointAndAngles.Add(new PointAndAngle() { x = minIntersectx, y = minIntersecty, angle = min_angle });
                //Debug.DrawRay(transform.position, minIntersect, Color.red);
            }
            else
            {
                pointAndAngles.Add(new PointAndAngle() { x = raydeltax, y = raydeltay });
                Debug.Log("intersect not found - draw anyway?");
                //Debug.DrawLine(transform.position, new Vector3(dx, dy));
            }
        }

        pointAndAngles.Sort();
        return pointAndAngles;
    }

    public float PseudoAngle(float x, float y)
    {
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);
        var p = y / (ax + ay);
        return x < 0 ? 2 - p : p;
    }

    public Segment[] FindAllLines()
    {
        var allwalls = GameObject.FindGameObjectsWithTag("wall").ToArray();
        List<Segment> retval = new List<Segment>();
        foreach (var gameObject in allwalls)
        {
            var collider = gameObject.GetComponent<BoxCollider2D>();
            var center = collider.offset;
            var extents = collider.size;
            var NorthEast = gameObject.transform.TransformPoint(center + new Vector2(extents.x, extents.y) * 0.5f);
            var SouthEast = gameObject.transform.TransformPoint(center + (new Vector2(extents.x, -extents.y) * 0.5f));///center + new Vector3(extents.x, -extents.y);
            var SouthWest = gameObject.transform.TransformPoint(center + (new Vector2(-extents.x, -extents.y) * 0.5f)); //center + new Vector3(-extents.x, -extents.y);
            var NorthWest = gameObject.transform.TransformPoint(center + (new Vector2(-extents.x, extents.y) * 0.5f));// center + new Vector3(-extents.x, extents.y);
            retval.Add(new Segment { a = NorthEast, b = SouthEast });
            retval.Add(new Segment { a = SouthEast, b = SouthWest });
            retval.Add( new Segment { a = SouthWest, b = NorthWest });
            retval.Add( new Segment { a = NorthWest, b = NorthEast });
        }
        var cam = Camera.main;
        var vert = cam.orthographicSize;//Camera.main.orthographicSize;
        var horz = vert * Screen.width / Screen.height;
        var camPos = Camera.main.transform.position;
        var northeast = new Vector3(camPos.x + vert, camPos.y + horz, 0);
        var southeast = new Vector3(camPos.x + vert, camPos.y - horz, 0);
        var southwest = new Vector3(camPos.x - vert, camPos.y - horz, 0);
        var northwest = new Vector3(camPos.x - vert, camPos.y + horz, 0);
        retval.Add(new Segment { a = northeast, b = southeast });
        retval.Add(new Segment { a = southeast, b = southwest });
        retval.Add(new Segment { a = southwest, b = northwest });
        retval.Add(new Segment { a = northwest, b = northeast });
        return retval.ToArray();
    }
    public struct Segment
    {
        public Vector2 a;
        public Vector2 b;
    }

    struct PointAndAngle : IComparable<PointAndAngle>
    {
        public float angle;
        public float x;
        public float y;


        public int CompareTo(PointAndAngle other)
        {
            return angle.CompareTo(other.angle);
        }
    }
}
