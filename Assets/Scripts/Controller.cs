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
    public float angleEpsilon = 0.005f;
    [SerializeField]
    float speed = 10;
    public float radius = 5f;

    public LayerMask mask;

    public Material mat;
    MeshRenderer renderer;


    ColliderVertices[] colliderVertices;
    Vector2[] AllPoints;
    float[] Angles;
    MeshFilter MeshFilter;
    List<PointAndAngle> pointAndAngles = new List<PointAndAngle>();//TODO: Don't new up here
    // Start is called before the first frame update
    void Start()
    {
        //TODO: Optimize this;
        GetAllPoints();
        Angles = new float[AllPoints.Length * 3];
    }

    private void GetAllPoints()
    {
        var points = new List<Vector2>();
        foreach (var item in colliderVertices.SelectMany(x => x.Vertices))
        {
            points.Add(item);
        }
        AllPoints = points.ToArray();
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

    // Update is called once per frame
    void Update()
    {
        FindAllWalls();
        GetAllPoints();
        //colliderVertices[colliderVertices.Length - 1] = new ColliderVertices(Camera.main);

        Movement();

        int angleIndex = 0;
        for (int i = 0; i < AllPoints.Length; i++)
        {
            var delta = (Vector3)AllPoints[i] - transform.position;
            float angle = Mathf.Atan2(delta.y, delta.x);
            Angles[angleIndex++] = (angle - angleEpsilon);
            Angles[angleIndex++] = (angle);
            Angles[angleIndex++] = (angle + angleEpsilon);
        }

        pointAndAngles.Clear();

        var origPos = transform.position;
        for (int i = 0; i < Angles.Length; i++)
        {
            var rayDirection = new Vector3(Mathf.Cos(Angles[i]), Mathf.Sin(Angles[i]));
            RaycastHit2D hit = Physics2D.Raycast(origPos, rayDirection, radius, mask);

            if (hit.collider != null)
            {
                // If we hit something, we use that point
                pointAndAngles.Add(new PointAndAngle() { Point = hit.point, angle = Angles[i] });
            }
            else
            {
                // If we didn't hit anything, we use the point on the edge of the circle
                pointAndAngles.Add(new PointAndAngle() { Point = origPos + rayDirection * radius, angle = Angles[i] });
            }
        }

        GenerateMesh();
    }

    private void Movement()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.position += speed * Time.deltaTime * Vector3.up;
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.position += speed * Time.deltaTime * Vector3.right;
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.position += -speed * Time.deltaTime * Vector3.up;
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.position += -speed * Time.deltaTime * Vector3.right;
        }
    }

    private void GenerateMesh()
    {
        pointAndAngles.Sort();
        //MESH GENERATION
        Vector3[] vertices = new Vector3[pointAndAngles.Count * 2 + 1];
        int[] triangles = new int[vertices.Length * 3 + 3];
        vertices[0] = (transform.InverseTransformPoint(transform.position)); // Wonder if conversion is needed

        for (int i = 1; i <= pointAndAngles.Count; i++)
        {
            vertices[i] = transform.InverseTransformPoint(pointAndAngles[i - 1].Point);
            vertices[i + 1] = transform.InverseTransformPoint(pointAndAngles[(i) % pointAndAngles.Count].Point);
        }

        int triangleIndex = 0;
        for (int i = 0; i < vertices.Length + 1; i++)
        {
            triangles[triangleIndex++] = (i + 1) % vertices.Length;
            triangles[triangleIndex++] = i % vertices.Length;
            triangles[triangleIndex++] = 0;
        }


        var mesh = new Mesh()
        {
            vertices = vertices,
            triangles = triangles,
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
