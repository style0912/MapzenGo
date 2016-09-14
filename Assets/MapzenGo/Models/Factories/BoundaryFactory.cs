﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapzenGo.Helpers;
using MapzenGo.Models.Enums;
using UniRx;
using UnityEngine;

namespace MapzenGo.Models.Factories
{
    public class BoundaryFactory : Factory
    {
        public override string XmlTag { get { return "boundaries"; } }
        [SerializeField]
        private Boundary.Settings _settings;

        public override void Start()
        {
            Query = (geo) => geo["geometry"]["type"].str == "LineString" || geo["geometry"]["type"].str == "MultiLineString";
        }

        protected override IEnumerable<MonoBehaviour> Create(Vector2d tileMercPos, JSONObject geo)
        {
            var kind = geo["properties"]["kind"].str.ConvertToEnum<BoundaryType>();
            if (_settings.AllSettings.Any(x => x.Type == kind))
            {
                var typeSettings = _settings.GetSettingsFor(kind);

                if (geo["geometry"]["type"].str == "LineString")
                {
                    var boundary = new GameObject("boundary").AddComponent<Boundary>();
                    var mesh = boundary.GetComponent<MeshFilter>().mesh;
                    var boundarEnds = new List<Vector3>();
                    var verts = new List<Vector3>();
                    var indices = new List<int>();

                    for (var i = 0; i < geo["geometry"]["coordinates"].list.Count; i++)
                    {
                        var c = geo["geometry"]["coordinates"][i];
                        var dotMerc = GM.LatLonToMeters(c[1].f, c[0].f);
                        var localMercPos = dotMerc - tileMercPos;
                        boundarEnds.Add(localMercPos.ToVector3());
                    }
                    SetProperties(geo, boundary, typeSettings);
                    CreateMesh(boundarEnds, typeSettings, ref verts, ref indices);
                    mesh.vertices = verts.ToArray();
                    mesh.triangles = indices.ToArray();
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    boundary.GetComponent<MeshRenderer>().material = typeSettings.Material;
                    boundary.transform.position += Vector3.up * Order;
                    yield return boundary;
                }
                else if (geo["geometry"]["type"].str == "MultiLineString")
                {
                    for (var i = 0; i < geo["geometry"]["coordinates"].list.Count; i++)
                    {
                        var boundary = new GameObject("Boundary").AddComponent<Boundary>();
                        var mesh = boundary.GetComponent<MeshFilter>().mesh;
                        var roadEnds = new List<Vector3>();
                        var verts = new List<Vector3>();
                        var indices = new List<int>();

                        roadEnds.Clear();
                        var c = geo["geometry"]["coordinates"][i];
                        for (var j = 0; j < c.list.Count; j++)
                        {
                            var seg = c[j];
                            var dotMerc = GM.LatLonToMeters(seg[1].f, seg[0].f);
                            var localMercPos = dotMerc - tileMercPos;
                            roadEnds.Add(localMercPos.ToVector3());
                        }
                        CreateMesh(roadEnds, typeSettings, ref verts, ref indices);
                        mesh.vertices = verts.ToArray();
                        mesh.triangles = indices.ToArray();
                        mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                        boundary.GetComponent<MeshRenderer>().material = typeSettings.Material;
                        //road.Initialize(geo, roadEnds, _settings);
                        boundary.transform.position += Vector3.up * Order;
                        yield return boundary;
                    }
                }
            }
        }

        protected override GameObject CreateLayer(Vector2d tileMercPos, List<JSONObject> geoList)
        {
            var go = new GameObject("Boundary");
            var mesh = go.AddComponent<MeshFilter>().mesh;
            go.AddComponent<MeshRenderer>();
            var verts = new List<Vector3>();
            var indices = new List<int>();

            GetVertices(tileMercPos, geoList, ref verts, ref indices);
            mesh.vertices = verts.ToArray();
            mesh.triangles = indices.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.GetComponent<MeshRenderer>().material = _settings.Default.Material;
            go.transform.position += Vector3.up * Order;
            return go;
        }

        private void GetVertices(Vector2d tileMercPos, List<JSONObject> geoList, ref List<Vector3> verts, ref List<int> indices)
        {
            foreach (var geo in geoList.Where(x => Query(x)))
            {
                var kind = geo["properties"]["kind"].str.ConvertToEnum<BoundaryType>();
                if (!_settings.HasSettingsFor(kind))
                    continue;

                var settings = _settings.GetSettingsFor(kind);
                var roadEnds = new List<Vector3>();

                if (geo["geometry"]["type"].str == "LineString")
                {
                    for (var i = 0; i < geo["geometry"]["coordinates"].list.Count; i++)
                    {
                        var c = geo["geometry"]["coordinates"][i];
                        var dotMerc = GM.LatLonToMeters(c[1].f, c[0].f);
                        var localMercPos = dotMerc - tileMercPos;
                        roadEnds.Add(localMercPos.ToVector3());
                    }
                    CreateMesh(roadEnds, settings, ref verts, ref indices);
                    //yield return CreateRoadSegment(geo, roadEnds);
                }
                else if (geo["geometry"]["type"].str == "MultiLineString")
                {
                    for (var i = 0; i < geo["geometry"]["coordinates"].list.Count; i++)
                    {
                        roadEnds.Clear();
                        var c = geo["geometry"]["coordinates"][i];
                        for (var j = 0; j < c.list.Count; j++)
                        {
                            var seg = c[j];
                            var dotMerc = GM.LatLonToMeters(seg[1].f, seg[0].f);
                            var localMercPos = dotMerc - tileMercPos;
                            roadEnds.Add(localMercPos.ToVector3());
                        }
                        CreateMesh(roadEnds, settings, ref verts, ref indices);
                    }
                }
            }
        }

        private void CreateMesh(List<Vector3> list, Boundary.BoundarySettings settings, ref List<Vector3> verts, ref List<int> indices)
        {
            var vertsStartCount = verts.Count;
            Vector3 lastPos = Vector3.zero;
            var norm = Vector3.zero;
            for (int i = 1; i < list.Count; i++)
            {
                var p1 = list[i - 1];
                var p2 = list[i];
                var p3 = p2;
                if (i + 1 < list.Count)
                    p3 = list[i + 1];

                if (lastPos == Vector3.zero)
                {
                    lastPos = Vector3.Lerp(p1, p2, 0f);
                    norm = GetNormal(p1, lastPos, p2) * settings.Width;
                    verts.Add(lastPos + norm);
                    verts.Add(lastPos - norm);
                }

                lastPos = Vector3.Lerp(p1, p2, 1f);
                norm = GetNormal(p1, lastPos, p3) * settings.Width;
                verts.Add(lastPos + norm);
                verts.Add(lastPos - norm);
            }


            for (int j = vertsStartCount; j <= verts.Count - 3; j += 2)
            {
                var clock = Vector3.Cross(verts[j + 1] - verts[j], verts[j + 2] - verts[j + 1]);
                if (clock.y < 0)
                {
                    indices.Add(j);
                    indices.Add(j + 2);
                    indices.Add(j + 1);

                    indices.Add(j + 1);
                    indices.Add(j + 2);
                    indices.Add(j + 3);
                }
                else
                {
                    indices.Add(j + 1);
                    indices.Add(j + 2);
                    indices.Add(j);

                    indices.Add(j + 3);
                    indices.Add(j + 2);
                    indices.Add(j + 1);
                }
            }
        }

        private static void SetProperties(JSONObject geo, Boundary boundary, Boundary.BoundarySettings typeSettings)
        {
            boundary.name = "boundary " + geo["properties"]["id"].ToString();
            if (geo["properties"].HasField("name"))
                boundary.Name = geo["properties"]["name"].str;

            boundary.Id = geo["properties"]["id"].ToString();
            boundary.Type = geo["type"].str;
            boundary.SortKey = (int)geo["properties"]["sort_key"].f;
            boundary.Kind = geo["properties"]["kind"].str;
            boundary.GetComponent<MeshRenderer>().material = typeSettings.Material;
        }

        private Vector3 GetNormal(Vector3 p1, Vector3 newPos, Vector3 p2)
        {
            if (newPos == p1 || newPos == p2)
            {
                var n = (p2 - p1).normalized;
                return new Vector3(-n.z, 0, n.x);
            }

            var b = (p2 - newPos).normalized + newPos;
            var a = (p1 - newPos).normalized + newPos;
            var t = (b - a).normalized;

            if (t == Vector3.zero)
            {
                var n = (p2 - p1).normalized;
                return new Vector3(-n.z, 0, n.x);
            }

            return new Vector3(-t.z, 0, t.x);
        }

    }
}
