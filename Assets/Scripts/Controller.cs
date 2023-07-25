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
public class Controller : MonoBehaviour
{
    Transform transform;
    [SerializeField]
    float speed = 10;
    public float radius = 5f;

    public LayerMask mask;

    public Material mat;
    MeshRenderer renderer;


    ColliderVertices[] colliderVertices;
    Segment[] Segments;
    Vector2[] AllPoints;
    List<DirectionAndAngle> directionAndAngles = new List<DirectionAndAngle>();
    MeshFilter MeshFilter;
    List<PointAndAngle> pointAndAngles = new List<PointAndAngle>();//TODO: Don't new up here
    // Start is called before the first frame update
    void Start()
    {
        transform = GetComponent<Transform>();
        Segments = FindAllLines();
        //TODO: Optimize this;
        var points = new List<Vector2>();
        foreach (var item in Segments)
        {
            points.Add(item.a);
            points.Add(item.b);
        }
        AllPoints = points.Distinct().ToArray();
    }
    private void Awake()
    {
        FindAllWalls();
        MeshFilter = GetComponentInChildren<MeshFilter>();
        renderer = GetComponentInChildren<MeshRenderer>();
        renderer.material = mat;
    }

    private void OnDrawGizmos()
    {
        foreach (var item in pointAndAngles)
        {
            Gizmos.DrawSphere(item.Point, 0.2f);
        }
    }

    List<float> angles = new List<float>(); //TODO: FIND BETTER;
    // Update is called once per frame
    void Update()
    {
        colliderVertices[colliderVertices.Length - 1] = new ColliderVertices(Camera.main);

        if (Input.GetKey(KeyCode.W))
        {
            transform.position += Vector3.up * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.position += Vector3.right * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.position += Vector3.up * -speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.position += Vector3.right * -speed * Time.deltaTime;
        }
        directionAndAngles.Clear();
        //directionAndAngles = colliderVertices.SelectMany(x => x.Vertices.Select(v => new DirectionAndAngle(transform.position, v))).OrderByDescending(d => d.angle).ToList();
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

        pointAndAngles.Clear();

        var origPos = transform.position;
        for (int i = 0; i < angles.Count; i++)
        {
            var raydeltax = radius * Mathf.Cos(angles[i]);
            var raydeltay = radius * Mathf.Sin(angles[i]);
            var min_t1 = float.MaxValue;
            Vector2 minIntersect = new Vector2();
            var min_angle = 0f;
            var found = false;
            Debug.DrawRay(transform.position, new Vector3(raydeltax, raydeltay), Color.yellow);
            for (int j = 0; j < Segments.Length; j++)
            {
                var segmentDelta = Segments[j].b - Segments[j].a;
                if (Mathf.Abs(segmentDelta.x - raydeltax) > 0 && Mathf.Abs(segmentDelta.y - raydeltay) > 0)
                {
                    var seg = Segments[j];
                    var t2 = (raydeltax * (seg.a.y - origPos.y) + (raydeltay * (origPos.x - seg.a.x))) / (segmentDelta.x * raydeltay - segmentDelta.y * raydeltax);
                    var t1 = (seg.a.x + segmentDelta.x * t2 - origPos.x) / raydeltax;
                    if (t1 > 0 && t2 >= 0 && t2<=1.0f)
                    {
                        if (t1<min_t1)
                        {
                            min_t1 = t1;
                            //TODO: Maybe not new em up here... Potential GC overload??
                            minIntersect = new Vector2(origPos.x + raydeltax * t1, origPos.y + raydeltay * t1);

                            //TODO: maybe not recalculate angle, just use that sort by angle without calculating?
                            min_angle = Mathf.Atan2(minIntersect.y - origPos.y, minIntersect.x - origPos.x);
                            found = true;
                        }
                    }
                }
            }
            if (found)
            {
                pointAndAngles.Add(new PointAndAngle() { Point = minIntersect, angle = min_angle });
                Debug.DrawRay(transform.position, minIntersect, Color.red);
            }
            else
            {
                pointAndAngles.Add(new PointAndAngle() { Point = new Vector3(raydeltax, raydeltay) });
                Debug.Log("intersect not found - draw anyway?");
                //Debug.DrawLine(transform.position, new Vector3(dx, dy));
            }
        }

        pointAndAngles.Sort();
        //MESH GENERATION
        Vector3[] vertices = new Vector3[pointAndAngles.Count * 2 + 1];
        List<int> triangles = new List<int>();
        //vertices.Clear();
        triangles.Clear();

        vertices[0] = (transform.InverseTransformPoint(transform.position)); // Wonder if conversion is needed

        for (int i = 1; i <= pointAndAngles.Count; i++)
        {
            vertices[i] = transform.InverseTransformPoint(pointAndAngles[i-1].Point);
            vertices[i + 1] = transform.InverseTransformPoint(pointAndAngles[(i) % pointAndAngles.Count].Point);
        }

        for (int i = 0; i < vertices.Length+1; i++)
        {
            triangles.Add((i + 1) % vertices.Length);
            triangles.Add((i) % vertices.Length);
            triangles.Add(0);
        }


        MeshFilter.mesh.Clear();
        var mesh = new Mesh()
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        MeshFilter.mesh = mesh;
    }
    public void FindAllWalls()
    {
        var retval = GameObject.FindGameObjectsWithTag("wall")
            .Select(x => new ColliderVertices(x.GetComponent<BoxCollider2D>()))
            .ToList();
        //TODO THIS SHIT IS DYNAMIC, CHANGE IT.
        retval.Add(new ColliderVertices(Camera.main));
        colliderVertices = retval.ToArray();
    }

    public Segment[] FindAllLines()
    {
        var allwalls = GameObject.FindGameObjectsWithTag("wall").Select(x=> x.GetComponent<BoxCollider2D>()).ToArray();
        List<Segment> retval = new List<Segment>();
        foreach (var collider in allwalls)
        {
            var center = collider.bounds.center;
            var extents = collider.bounds.extents;
            var NorthEast = center + extents;
            var SouthEast = center + new Vector3(extents.x, -extents.y);
            var SouthWest = center + new Vector3(-extents.x, -extents.y);
            var NorthWest = center + new Vector3(-extents.x, extents.y);
            retval.Add(new Segment { a = NorthEast, b = SouthEast });
            retval.Add(new Segment { a = SouthEast, b = SouthWest });
            retval.Add( new Segment { a = SouthWest, b = NorthWest });
            retval.Add( new Segment { a = NorthWest, b = NorthEast });
        }
        var cam = Camera.main;
        var vert = cam.orthographicSize;//Camera.main.orthographicSize;
        var horz = vert * Screen.width / Screen.height;
        var camPos = Camera.main.transform.position;
        var northeast =  new Vector3(camPos.x + vert, camPos.y + horz, 0);
        var southeast = new Vector3(camPos.x + vert, camPos.y - horz, 0);
        var southwest = new Vector3(camPos.x - vert, camPos.y - horz, 0);
        var northwest = new Vector3(camPos.x - vert, camPos.y + horz, 0);
        retval.Add(new Segment { a = northeast, b = southeast });
        retval.Add( new Segment { a = southeast, b = southwest });
        retval.Add( new Segment { a = southwest, b = northwest });
        retval.Add( new Segment { a = northwest, b = northeast });
        return retval.ToArray();
    }
    public struct Segment
    {
        public Vector2 a;
        public Vector2 b;
    }

    struct ColliderVertices
    {
        public Vector3[] Vertices;

        public ColliderVertices(BoxCollider2D collider)
        {
            Vertices = new Vector3[4];
            var center = collider.bounds.center;
            var extents = collider.bounds.extents;
            Vertices[0] = center + extents;
            Vertices[1] = center + new Vector3(extents.x, -extents.y);
            Vertices[2] = center + new Vector3(-extents.x, -extents.y);
            Vertices[3] = center + new Vector3(-extents.x, extents.y);
        }

        public ColliderVertices(Camera cam)
        {

            var vert = cam.orthographicSize;//Camera.main.orthographicSize;
            var horz = vert * Screen.width / Screen.height;
            var camPos = Camera.main.transform.position;
            Vertices = new Vector3[4];
            Vertices[0] =  new Vector3(camPos.x + vert, camPos.y + horz, 0);
            Vertices[1] = new Vector3(camPos.x + vert, camPos.y - horz, 0);
            Vertices[2] = new Vector3(camPos.x - vert, camPos.y - horz, 0);
            Vertices[3] = new Vector3(camPos.x - vert, camPos.y + horz, 0);
        }
    }

    struct PointAndAngle : IComparable<PointAndAngle>
    {
        public float angle;
        public Vector3 Point;

        public int CompareTo(PointAndAngle other)
        {
            return angle.CompareTo(other.angle);
        }
    }

    struct DirectionAndAngle : IComparable<DirectionAndAngle>
    {
        public float angle;
        public Vector3 direction;
        public DirectionAndAngle(Vector3 from, Vector3 to)
        {
            direction = to - from;
            angle = Mathf.Atan2(direction.y, direction.x);
        }

        public int CompareTo(DirectionAndAngle other)
        {
            return angle.CompareTo(other.angle);
        }
    }
}
