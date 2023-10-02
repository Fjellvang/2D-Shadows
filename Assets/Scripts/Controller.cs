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

    public LayerMask mask;

    public Material mat;
    MeshRenderer _renderer;

    Vector2[] _allPoints;

    float[] _angles;
    MeshFilter _meshFilter;
    List<PointAndAngle> _pointAndAngles = new List<PointAndAngle>();

    Collider2D[] _preallocatedColliders = new Collider2D[16]; // Is 16 enough?
    Segment[] _segments;


    private void CalculatePointsAndAngles(Collider2D[] staticColliders, int hitCount)
    {
        PopulateAllSegmentsAndPoints(staticColliders, hitCount);
        //_angles = new float[_allPoints.Count * 3];
    }

    private void Awake()
    {
        //Add 4 to each to account for the vision bounds
        _segments = new Segment[_preallocatedColliders.Length * 4 + 4]; // 4 points per collider, since we only use bounding box for now
        _allPoints = new Vector2[_preallocatedColliders.Length * 4 + 4]; // 4 points per collider

        _angles = new float[_allPoints.Length * 3]; // 3 angles per point

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
        //var colliders = Physics2D.OverlapBoxAll(transform.position, VisionBounds.size, 360, mask);

        _pointAndAngles.Clear();
        var hitcount = Physics2D.OverlapBoxNonAlloc(transform.position, VisionBounds.size, 360, _preallocatedColliders, mask);
        CalculatePointsAndAngles(_preallocatedColliders, hitcount);


        Movement();

        int angleIndex = 0;
        for (int i = 0; i < hitcount + 4; i++)
        {
            var delta = (Vector3)_allPoints[i] - transform.position;
            float angle = Mathf.Atan2(delta.y, delta.x);
            _angles[angleIndex++] = (angle - 0.001f);
            _angles[angleIndex++] = (angle);
            _angles[angleIndex++] = (angle + 0.001f);
        }

        var origPos = transform.position;
        for (int i = 0; i < hitcount * 3 + 12; i++)
        {
            // Custom raycasting
            var raydeltax = Mathf.Cos(_angles[i]);
            var raydeltay = Mathf.Sin(_angles[i]);
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
                var t2 = 
                        (raydeltax * (seg.a.y - origPos.y) + (raydeltay * (origPos.x - seg.a.x))) / 
                        (segmentDelta.x * raydeltay - segmentDelta.y * raydeltax);
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
                _pointAndAngles.Add(new PointAndAngle(minIntersect, _angles[i]));
            }
            else
            {
                _pointAndAngles.Add(new PointAndAngle(new Vector3(raydeltax, raydeltay)));
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
        if (Input.GetKey(KeyCode.E))
        {
            transform.rotation *= Quaternion.Euler(new Vector3(0, 0, -speed * 5 * Time.deltaTime));
        }
        if (Input.GetKey(KeyCode.Q))
        {
            transform.rotation *= Quaternion.Euler(new Vector3(0, 0, speed * 5 * Time.deltaTime));
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


    public void PopulateAllSegmentsAndPoints(Collider2D[] colliders, int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            var collider = colliders[i];
            CalculateBoxColliderSegmentsAndPoints(collider, i * 4);
        }

        if (hitCount == 0)
        {
            CalculateBoxColliderSegmentsAndPoints(VisionBounds, 4);
        }
        CalculateBoxColliderSegmentsAndPoints(VisionBounds, hitCount * 4);
    }

    private void CalculateBoxColliderSegmentsAndPoints(Collider2D collider, int index)
    {
        var center = collider.bounds.center;
        var extents = collider.bounds.extents;

        var northEast = center + extents;
        var southEast = center + new Vector3(extents.x, -extents.y);
        var southWest = center + new Vector3(-extents.x, -extents.y);
        var northWest = center + new Vector3(-extents.x, extents.y);

        _segments[index] = new Segment(northEast, southEast);
        _segments[index + 1] = (new Segment(southEast, southWest));
        _segments[index + 2] = (new Segment(southWest, northWest));
        _segments[index + 3] = (new Segment(northWest, northEast));

        _allPoints[index] = northEast;
        _allPoints[index + 1] = southEast;
        _allPoints[index + 2] = southWest;
        _allPoints[index + 3] = northWest;
    }

    public readonly struct Segment
    {
        public Segment(Vector2 a, Vector2 b)
        {
            this.a = a;
            this.b = b;
        }
        public readonly Vector2 a;
        public readonly Vector2 b;
    }

    readonly struct ColliderVertices
    {
        public readonly Vector3[] Vertices;

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

            var vert = cam.orthographicSize;
            var horz = vert * Screen.width / Screen.height;
            var camPos = Camera.main.transform.position;
            Vertices = new Vector3[4];
            Vertices[0] = new Vector3(camPos.x + vert, camPos.y + horz, 0);
            Vertices[1] = new Vector3(camPos.x + vert, camPos.y - horz, 0);
            Vertices[2] = new Vector3(camPos.x - vert, camPos.y - horz, 0);
            Vertices[3] = new Vector3(camPos.x - vert, camPos.y + horz, 0);
        }
    }

    readonly struct PointAndAngle : IComparable<PointAndAngle>
    {
        public readonly float angle;
        public readonly Vector3 Point;

        public PointAndAngle(Vector3 point, float angle = 0)
        {
            this.angle = angle;
            Point = point;
        }

        public int CompareTo(PointAndAngle other)
        {
            return angle.CompareTo(other.angle);
        }
    }

    readonly struct DirectionAndAngle : IComparable<DirectionAndAngle>
    {
        public readonly float angle;
        public readonly Vector3 direction;
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
