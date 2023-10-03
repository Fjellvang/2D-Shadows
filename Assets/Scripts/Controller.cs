using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Inspired from https://github.com/OneLoneCoder/olcPixelGameEngine/blob/master/OneLoneCoder_PGE_ShadowCasting2D.cpp
/// 
/// needs heavy refactoring
/// </summary>
public class Controller : MonoBehaviour
{
    public Vector3 VisionBoundsExtends = new Vector3(5, 5);

    [SerializeField]
    float _speed = 10;
    public float radius = 5f;

    public LayerMask mask;

    public Material mat;
    MeshRenderer _renderer;


    Segment[] _segments;
    List<Vector2> _allPoints = new List<Vector2>();

    float[] _angles;
    MeshFilter _meshFilter;
    List<PointAndAngle> _pointAndAngles = new List<PointAndAngle>();

    private void CalculatePointsAndAngles(Collider2D[] staticColliders)
    {
        _segments = FindAllLines(staticColliders);
        _angles = new float[_allPoints.Count * 3];
    }

    private void Awake()
    {
        _meshFilter = GetComponentInChildren<MeshFilter>();
        _renderer = GetComponentInChildren<MeshRenderer>();
        _renderer.material = mat;
    }

    private void OnDrawGizmos()
    {
        foreach (var item in _pointAndAngles)
        {

            Gizmos.DrawSphere(item.Point, 0.2f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        var colliders = Physics2D.OverlapBoxAll(transform.position, VisionBoundsExtends, 360, mask);
        CalculatePointsAndAngles(colliders);

        Movement();

        int angleIndex = 0;
        for (int i = 0; i < _allPoints.Count; i++)
        {
            var delta = (Vector3)_allPoints[i] - transform.position;
            float angle = Mathf.Atan2(delta.y, delta.x);
            _angles[angleIndex++] = (angle - 0.001f);
            _angles[angleIndex++] = (angle);
            _angles[angleIndex++] = (angle + 0.001f);
        }

        _pointAndAngles.Clear();

        var origPos = transform.position;
        for (int i = 0; i < _angles.Length; i++)
        {
            // Custom raycasting
            var raydeltax = radius * Mathf.Cos(_angles[i]);
            var raydeltay = radius * Mathf.Sin(_angles[i]);
            var min_t1 = float.MaxValue;
            Vector2 minIntersect = new Vector2();
            var found = false;
            // Check the ray against all segments within view. 
            // This could most likely be optimized using a quadtree
            for (int j = 0; j < _segments.Length; j++)
            {
                var seg = _segments[j];
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
                _pointAndAngles.Add(new PointAndAngle() { Point = minIntersect, angle = _angles[i] });
            }
            else
            {
                _pointAndAngles.Add(new PointAndAngle() { Point = new Vector3(raydeltax, raydeltay) });
            }
        }

        GenerateMesh();
    }

    private void Movement()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.position += _speed * Time.deltaTime * Vector3.up;
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.position += _speed * Time.deltaTime * Vector3.right;
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.position += -_speed * Time.deltaTime * Vector3.up;
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.position += -_speed * Time.deltaTime * Vector3.right;
        }
    }

    private void GenerateMesh()
    {
        _pointAndAngles.Sort();
        //MESH GENERATION
        Vector3[] vertices = new Vector3[_pointAndAngles.Count * 2 + 1];
        int[] triangles = new int[vertices.Length * 3];
        Vector2[] uvs = new Vector2[vertices.Length];
        vertices[0] = (transform.InverseTransformPoint(transform.position)); // Wonder if conversion is needed

        for (int i = 1; i <= _pointAndAngles.Count; i++)
        {
            vertices[i] = transform.InverseTransformPoint(_pointAndAngles[i - 1].Point);
            vertices[i + 1] = transform.InverseTransformPoint(_pointAndAngles[(i) % _pointAndAngles.Count].Point);
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

        _meshFilter.mesh = mesh;
    }


    public Segment[] FindAllLines(Collider2D[] staticColliders)
    {
        _allPoints.Clear();
        List<Segment> segments = new List<Segment>();
        foreach (var collider in staticColliders)
        {
            CalculateBoxColliderSegments(segments, collider.bounds.center, collider.bounds.extents);
        }

        CalculateBoxColliderSegments(segments, transform.position, VisionBoundsExtends);


        return segments.ToArray();
    }

    private void CalculateBoxColliderSegments(List<Segment> existingSegments, Vector3 center, Vector3 extents)
    {
        var northEast = center + extents;
        var southEast = center + new Vector3(extents.x, -extents.y);
        var southWest = center + new Vector3(-extents.x, -extents.y);
        var northWest = center + new Vector3(-extents.x, extents.y);

        existingSegments.Add(new Segment { a = northEast, b = southEast });
        existingSegments.Add(new Segment { a = southEast, b = southWest });
        existingSegments.Add(new Segment { a = southWest, b = northWest });
        existingSegments.Add(new Segment { a = northWest, b = northEast });

        _allPoints.Add(northEast);
        _allPoints.Add(southEast);
        _allPoints.Add(southWest);
        _allPoints.Add(northWest);
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
