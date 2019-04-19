using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class VisibilityScript : MonoBehaviour
{
    BoxCollider2D Collider;
    [SerializeField]
    GameObject Target;
    [SerializeField]
    float RayDistance = 5;
    [SerializeField]
    float Epsilon = 1.1f;

    MeshFilter filter;
    List<Vector2> Points = new List<Vector2>();
    List<Vector2> Points2 = new List<Vector2>();
    private void Awake()
    {
        Collider = GetComponent<BoxCollider2D>();
        filter = GetComponentInChildren<MeshFilter>();
    }
    // Update is called once per frame
    void Update()
    {
        Points.Clear();
        Points2.Clear();

        var bounds = Collider.bounds;
        var TopRight = bounds.center + bounds.extents * Epsilon;
        var TopLeft = bounds.center + new Vector3(-bounds.extents.x, bounds.extents.y) * Epsilon;
        var BottomRight = bounds.center + new Vector3(bounds.extents.x, -bounds.extents.y) * Epsilon;
        var BottomLeft = bounds.center + new Vector3(-bounds.extents.x, -bounds.extents.y) * Epsilon;

        CheckPlayerVisibility(TopRight);
        CheckPlayerVisibility(BottomRight);
        CheckPlayerVisibility(BottomLeft);
        CheckPlayerVisibility(TopLeft);


        Points2.Reverse();
        var _vertices = Points.Concat(Points2).ToArray();
        var vec3 = System.Array.ConvertAll<Vector2, Vector3>(_vertices, x => x);
        var Triangulator = new Triangulator(_vertices);
        var indices = Triangulator.Triangulate();
        //Array.Reverse(indices);
        Debug.Log($"Vertices: {string.Join(",", vec3)}, TopRight: {TopRight}, BottomRight{BottomRight}");
        filter.mesh.Clear();
        var mesh = new Mesh()
        {
            vertices = vec3,
            triangles = indices
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        filter.mesh = mesh;
    }

    private void CheckPlayerVisibility(Vector3 origin)
    {
        var hit = Physics2D.Linecast(origin, Target.transform.position);

        if (hit && hit.collider.tag.Equals("Player", StringComparison.Ordinal))
        {
            Debug.DrawLine(origin, Target.transform.position, Color.green);
            var delta = Target.transform.position - origin;
            Debug.DrawLine(origin, origin - delta, Color.cyan);
            Points.Add(origin);
            Points2.Add(origin - delta);
            //Debug.DrawLine(origin, Vector3.Reflect(Target.transform.position, -Vector3.right), Color.red);
        }
    }
}
