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
    public BoxCollider2D VisionBounds;

    [SerializeField]
    float speed = 10;
    public float radius = 5f;

    public LayerMask mask;

    public Material mat;
    MeshRenderer renderer;


    Segment[] Segments;
    List<Vector2> AllPoints = new List<Vector2>();

    float[] Angles;
    MeshFilter MeshFilter;
    List<PointAndAngle> pointAndAngles = new List<PointAndAngle>();//TODO: Don't new up here
    List<BoxCollider2D> _staticBoxColliders;
    // Start is called before the first frame update
    void Start()
    {
        _staticBoxColliders = GameObject.FindGameObjectsWithTag("wall").Select(x => x.GetComponent<BoxCollider2D>()).ToList();
        CalculatePointsAndAngles(_staticBoxColliders);
    }

    private void CalculatePointsAndAngles(List<BoxCollider2D> staticColliders)
    {
        Segments = FindAllLines(staticColliders);
        Angles = new float[AllPoints.Count * 3];
    }

    private void Awake()
    {
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

    // Update is called once per frame
    void Update()
    {
        CalculatePointsAndAngles(_staticBoxColliders);

        Movement();

        int angleIndex = 0;
        for (int i = 0; i < AllPoints.Count; i++)
        {
            var delta = (Vector3)AllPoints[i] - transform.position;
            float angle = Mathf.Atan2(delta.y, delta.x);
            Angles[angleIndex++] = (angle - 0.001f);
            Angles[angleIndex++] = (angle);
            Angles[angleIndex++] = (angle + 0.001f);
        }

        pointAndAngles.Clear();

        var origPos = transform.position;
        for (int i = 0; i < Angles.Length; i++)
        {
            // Custom raycasting
            var raydeltax = radius * Mathf.Cos(Angles[i]);
            var raydeltay = radius * Mathf.Sin(Angles[i]);
            var min_t1 = float.MaxValue;
            Vector2 minIntersect = new Vector2();
            var found = false;
            // Check the ray against all segments within view. 
            // This could most likely be optimized using a quadtree
            for (int j = 0; j < Segments.Length; j++)
            {
                var seg = Segments[j];
                var segmentDelta = seg.b - seg.a;

                // check if the lines are parrallel or coincident
                if (Mathf.Abs(segmentDelta.x - raydeltax) <= 0 || Mathf.Abs(segmentDelta.y - raydeltay) <= 0)
                {
                    continue;
                }

                // parametric equation if a ray and a line intersects
                var t2 = (raydeltax * (seg.a.y - origPos.y) + (raydeltay * (origPos.x - seg.a.x))) / (segmentDelta.x * raydeltay - segmentDelta.y * raydeltax);
                var t1 = (seg.a.x + segmentDelta.x * t2 - origPos.x) / raydeltax;
                // if t1 is less than 0, the ray is pointing in the wrong direction.
                // if t2 is not within 0 & 1 we have no intersection. hence we can continue.
                if (t1 <= 0 || t2 < 0 || t2 > 1.0f)
                {
                    continue;
                }

                // if the newly found intersection is less than the previous one, update our min intersection.
                // IE we want to find the point closets to the rays origin.
                if (t1 < min_t1)
                {
                    min_t1 = t1;

                    minIntersect = new Vector2(origPos.x + raydeltax * t1, origPos.y + raydeltay * t1);

                    found = true;
                }
            }
            if (found)
            {
                pointAndAngles.Add(new PointAndAngle() { Point = minIntersect, angle = Angles[i] });
            }
            else
            {
                pointAndAngles.Add(new PointAndAngle() { Point = new Vector3(raydeltax, raydeltay) });
            }
        }

        GenerateMesh();
    }

    private void Movement()
    {
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
    }

    private void GenerateMesh()
    {
        pointAndAngles.Sort();
        //MESH GENERATION
        Vector3[] vertices = new Vector3[pointAndAngles.Count * 2 + 1];
        int[] triangles = new int[vertices.Length * 3];
        Vector2[] uvs = new Vector2[vertices.Length];
        vertices[0] = (transform.InverseTransformPoint(transform.position)); // Wonder if conversion is needed

        for (int i = 1; i <= pointAndAngles.Count; i++)
        {
            vertices[i] = transform.InverseTransformPoint(pointAndAngles[i - 1].Point);
            vertices[i + 1] = transform.InverseTransformPoint(pointAndAngles[(i) % pointAndAngles.Count].Point);
        }

        int triangleIndex = 0;
        for (int i = 0; i < vertices.Length ; i++)
        {
            triangles[triangleIndex++] = (i + 1) % vertices.Length;
            triangles[triangleIndex++] = i % vertices.Length;
            triangles[triangleIndex++] = 0;
            // the + 5 is hald the bounds size, the / 10 is the bounds size
            uvs[i] = new Vector2((vertices[i].x+5)/10, (vertices[i].y+5)/10);
        }


        var mesh = new Mesh()
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        MeshFilter.mesh = mesh;
    }


    public Segment[] FindAllLines(List<BoxCollider2D> staticColliders)
    {
        AllPoints.Clear();
        List<Segment> segments = new List<Segment>();
        foreach (var collider in staticColliders)
        {
            CalculateBoxColliderSegments(segments, collider);
        }

        CalculateBoxColliderSegments(segments, VisionBounds);


        return segments.ToArray();
    }

    private void CalculateBoxColliderSegments(List<Segment> existingSegments, BoxCollider2D collider)
    {
        var center = collider.bounds.center;
        var extents = collider.bounds.extents;

        var northEast = center + extents;
        var southEast = center + new Vector3(extents.x, -extents.y);
        var southWest = center + new Vector3(-extents.x, -extents.y);
        var northWest = center + new Vector3(-extents.x, extents.y);

        existingSegments.Add(new Segment { a = northEast, b = southEast });
        existingSegments.Add(new Segment { a = southEast, b = southWest });
        existingSegments.Add(new Segment { a = southWest, b = northWest });
        existingSegments.Add(new Segment { a = northWest, b = northEast });

        AllPoints.Add(northEast);
        AllPoints.Add(southEast);
        AllPoints.Add(southWest);
        AllPoints.Add(northWest);
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
