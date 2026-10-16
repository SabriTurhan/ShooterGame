using System.Collections.Generic;
using UnityEngine;

namespace Watermelon.SquadShooter.Examples
{
    // Palette bands of player_atlas.png: each band is a 30px tall strip, light (left) -> dark (right)
    public static class Band
    {
        public const int Green = 0, Yellow = 1, White = 2, Black = 3, Red = 4, Blue = 5, Steel = 6, Purple = 7,
            Indigo = 8, Tan = 9, Pale = 10, Navy = 11, Plum = 12, Cream = 13, Gold = 14, Ice = 15, WarmGrey = 16,
            Sky = 17, Teal = 18, Mauve = 19, DarkPlum = 20, Brown = 21, Salmon = 22, Orange = 23, Grey = 24,
            Lavender = 26, DeepIndigo = 27;

        public const int ATLAS_SIZE = 1024;
        public const int BAND_HEIGHT = 30;

        // shade: 0 = lightest swatch, 1 = darkest swatch
        public static Vector2 UV(int band, float shade)
        {
            float x = Mathf.Lerp(8f, 168f, Mathf.Clamp01(shade)) / ATLAS_SIZE;
            float y = 1f - (band * BAND_HEIGHT + BAND_HEIGHT * 0.5f) / ATLAS_SIZE;

            return new Vector2(x, y);
        }

        public static int FromUV(Vector2 uv)
        {
            return Mathf.FloorToInt((1f - uv.y) * ATLAS_SIZE / BAND_HEIGHT);
        }

        public static Vector2 ShiftBand(Vector2 uv, int fromBand, int toBand)
        {
            uv.y -= (toBand - fromBand) * (float)BAND_HEIGHT / ATLAS_SIZE;

            return uv;
        }
    }

    // Builds flat shaded low-poly meshes out of convex primitives, coloured through the shared palette atlas
    public class PaletteMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public PaletteMesh Box(Vector3 size, float bevel, Vector3 position, Vector3 euler, int band, float light = 0.15f, float dark = 0.55f)
        {
            return Add(Shapes.Box(size, bevel), position, euler, Vector3.one, band, light, dark);
        }

        public PaletteMesh Cylinder(float radiusBottom, float radiusTop, float height, int sides, Vector3 position, Vector3 euler, int band, float light = 0.15f, float dark = 0.55f)
        {
            return Add(Shapes.Cylinder(radiusBottom, radiusTop, height, sides), position, euler, Vector3.one, band, light, dark);
        }

        public PaletteMesh Sphere(float radius, int rings, int segments, Vector3 position, Vector3 scale, int band, float light = 0.15f, float dark = 0.55f)
        {
            return Add(Shapes.Sphere(radius, rings, segments, false), position, Vector3.zero, scale, band, light, dark);
        }

        public PaletteMesh Dome(float radius, int rings, int segments, Vector3 position, Vector3 euler, Vector3 scale, int band, float light = 0.15f, float dark = 0.55f)
        {
            return Add(Shapes.Sphere(radius, rings, segments, true), position, euler, scale, band, light, dark);
        }

        public PaletteMesh Add(List<Vector3[]> polygons, Vector3 position, Vector3 euler, Vector3 scale, int band, float light, float dark)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (Vector3[] polygon in polygons)
            {
                foreach (Vector3 point in polygon)
                {
                    center += point;
                    count++;
                    minY = Mathf.Min(minY, point.y);
                    maxY = Mathf.Max(maxY, point.y);
                }
            }

            center /= Mathf.Max(1, count);

            Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);
            Matrix4x4 normalMatrix = matrix.inverse.transpose;

            foreach (Vector3[] polygon in polygons)
            {
                if (polygon.Length < 3) continue;

                Vector3 faceCenter = Vector3.zero;
                foreach (Vector3 point in polygon) faceCenter += point;
                faceCenter /= polygon.Length;

                Vector3 normal = Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]);
                if (normal.sqrMagnitude < 1e-12f) continue;

                // convex shapes: make every face point away from the shape center
                bool flip = Vector3.Dot(normal, faceCenter - center) < 0;
                Vector3 worldNormal = normalMatrix.MultiplyVector(flip ? -normal : normal).normalized;

                int start = vertices.Count;
                for (int i = 0; i < polygon.Length; i++)
                {
                    Vector3 point = polygon[flip ? polygon.Length - 1 - i : i];
                    float height01 = maxY - minY > 1e-5f ? (point.y - minY) / (maxY - minY) : 0.5f;

                    vertices.Add(matrix.MultiplyPoint3x4(point));
                    normals.Add(worldNormal);
                    uvs.Add(Band.UV(band, Mathf.Lerp(dark, light, height01)));
                }

                for (int i = 1; i < polygon.Length - 1; i++)
                {
                    triangles.Add(start);
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
            }

            return this;
        }

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }

    public static class Shapes
    {
        // Chamfered box: 6 faces, 12 bevel strips and 8 corner triangles
        public static List<Vector3[]> Box(Vector3 size, float bevel)
        {
            List<Vector3[]> polygons = new List<Vector3[]>();
            Vector3 half = size * 0.5f;
            float b = Mathf.Clamp(bevel, 0f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.9f);

            Vector3 Corner(int sx, int sy, int sz, int axis)
            {
                int[] signs = { sx, sy, sz };
                Vector3 point = new Vector3(sx * (half.x - b), sy * (half.y - b), sz * (half.z - b));
                point[axis] = signs[axis] * half[axis];

                return point;
            }

            int[] cycleU = { -1, 1, 1, -1 };
            int[] cycleV = { -1, -1, 1, 1 };

            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3[] face = new Vector3[4];
                    for (int i = 0; i < 4; i++)
                    {
                        int[] signs = new int[3];
                        signs[axis] = s;
                        signs[u] = cycleU[i];
                        signs[v] = cycleV[i];
                        face[i] = Corner(signs[0], signs[1], signs[2], axis);
                    }

                    polygons.Add(face);
                }
            }

            if (b <= 0f) return polygons;

            for (int a = 0; a < 3; a++)
            {
                for (int c = a + 1; c < 3; c++)
                {
                    int d = 3 - a - c;

                    for (int sa = -1; sa <= 1; sa += 2)
                    {
                        for (int sc = -1; sc <= 1; sc += 2)
                        {
                            int[] low = new int[3], high = new int[3];
                            low[a] = high[a] = sa;
                            low[c] = high[c] = sc;
                            low[d] = -1;
                            high[d] = 1;

                            polygons.Add(new Vector3[]
                            {
                                Corner(low[0], low[1], low[2], a),
                                Corner(high[0], high[1], high[2], a),
                                Corner(high[0], high[1], high[2], c),
                                Corner(low[0], low[1], low[2], c),
                            });
                        }
                    }
                }
            }

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        polygons.Add(new Vector3[] { Corner(sx, sy, sz, 0), Corner(sx, sy, sz, 1), Corner(sx, sy, sz, 2) });

            return polygons;
        }

        // Frustum along Y, centered on the origin; radiusTop = 0 gives a cone
        public static List<Vector3[]> Cylinder(float radiusBottom, float radiusTop, float height, int sides)
        {
            List<Vector3[]> polygons = new List<Vector3[]>();
            float h = height * 0.5f;
            Vector3[] bottom = new Vector3[sides];
            Vector3[] top = new Vector3[sides];

            for (int i = 0; i < sides; i++)
            {
                float angle = (i + 0.5f) / sides * Mathf.PI * 2f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottom[i] = direction * radiusBottom - Vector3.up * h;
                top[i] = direction * radiusTop + Vector3.up * h;
            }

            bool cone = radiusTop < 1e-4f;

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;

                if (cone)
                    polygons.Add(new Vector3[] { bottom[i], bottom[j], Vector3.up * h });
                else
                    polygons.Add(new Vector3[] { bottom[i], bottom[j], top[j], top[i] });
            }

            polygons.Add(bottom);
            if (!cone) polygons.Add(top);

            return polygons;
        }

        // Low-poly UV sphere; hemisphere = true keeps the upper half with a flat base
        public static List<Vector3[]> Sphere(float radius, int rings, int segments, bool hemisphere)
        {
            List<Vector3[]> polygons = new List<Vector3[]>();
            int ringCount = Mathf.Max(2, rings);
            float minLatitude = hemisphere ? 0f : -Mathf.PI * 0.5f;

            Vector3 Point(int ring, int segment)
            {
                float latitude = Mathf.Lerp(minLatitude, Mathf.PI * 0.5f, ring / (float)ringCount);
                float longitude = (segment + 0.5f) / segments * Mathf.PI * 2f;

                return new Vector3(Mathf.Cos(latitude) * Mathf.Cos(longitude), Mathf.Sin(latitude), Mathf.Cos(latitude) * Mathf.Sin(longitude)) * radius;
            }

            for (int ring = 0; ring < ringCount; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;

                    if (ring == ringCount - 1)
                        polygons.Add(new Vector3[] { Point(ring, segment), Point(ring, next), Vector3.up * radius });
                    else if (ring == 0 && !hemisphere)
                        polygons.Add(new Vector3[] { Vector3.down * radius, Point(ring + 1, next), Point(ring + 1, segment) });
                    else
                        polygons.Add(new Vector3[] { Point(ring, segment), Point(ring, next), Point(ring + 1, next), Point(ring + 1, segment) });
                }
            }

            if (hemisphere)
            {
                Vector3[] cap = new Vector3[segments];
                for (int segment = 0; segment < segments; segment++) cap[segment] = Point(0, segment);
                polygons.Add(cap);
            }

            return polygons;
        }
    }
}
