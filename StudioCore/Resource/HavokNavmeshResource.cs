﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Veldrid;
using Veldrid.Utilities;
using SoulsFormats;
using HKX2;
using System.IO;

namespace StudioCore.Resource
{
    public class HavokNavmeshResource : IResource, IDisposable
    {
        public int IndexCount;
        public int[] PickingIndices;

        public Scene.VertexIndexBufferAllocator.VertexIndexBufferHandle GeomBuffer { get; set; }

        public int VertexCount { get; set; }

        public Vector3[] PickingVertices;

        public hkRootLevelContainer HkxRoot = null;


        public BoundingBox Bounds { get; set; }

        unsafe private void ProcessMesh(hkaiNavMesh mesh)
        {
            var verts = mesh.m_vertices;
            int indexCount = 0;
            foreach (var f in mesh.m_faces)
            {
                // Simple formula for indices count for a triangulation of a poly
                indexCount += (f.m_numEdges - 2) * 3;
            }

            var MeshIndices = new int[indexCount * 3];
            var MeshVertices = new NavmeshLayout[indexCount * 3];
            //PickingVertices = new Vector3[mesh.Triangles.Count * 3];
            //PickingIndices = new int[mesh.Triangles.Count * 3];

            var factory = Scene.Renderer.Factory;

            int idx = 0;

            for (int id = 0; id < mesh.m_faces.Count; id++)
            {
                var sedge = mesh.m_faces[id].m_startEdgeIndex;
                var ecount = mesh.m_faces[id].m_numEdges;

                // Use simple algorithm for convex polygon trianglization
                for (int t = 0; t < ecount - 2; t++)
                {
                    var end = (sedge + t + 2 >= ecount) ? 0 : sedge + t + 2;
                    var vert1 = mesh.m_vertices[mesh.m_edges[sedge].m_a];
                    var vert2 = mesh.m_vertices[mesh.m_edges[sedge + t + 1].m_a];
                    var vert3 = mesh.m_vertices[mesh.m_edges[end].m_a];

                    MeshVertices[idx] = new NavmeshLayout();
                    MeshVertices[idx + 1] = new NavmeshLayout();
                    MeshVertices[idx + 2] = new NavmeshLayout();

                    MeshVertices[idx].Position = new Vector3(vert1.X, vert1.Y, vert1.Z);
                    MeshVertices[idx + 1].Position = new Vector3(vert2.X, vert2.Y, vert2.Z);
                    MeshVertices[idx + 2].Position = new Vector3(vert3.X, vert3.Y, vert3.Z);
                    PickingVertices[idx] = new Vector3(vert1.X, vert1.Y, vert1.Z);
                    PickingVertices[idx + 1] = new Vector3(vert2.X, vert2.Y, vert2.Z);
                    PickingVertices[idx + 2] = new Vector3(vert3.X, vert3.Y, vert3.Z);
                    var n = Vector3.Normalize(Vector3.Cross(MeshVertices[idx + 2].Position - MeshVertices[idx].Position, MeshVertices[idx + 1].Position - MeshVertices[idx].Position));
                    MeshVertices[idx].Normal[0] = (sbyte)(n.X * 127.0f);
                    MeshVertices[idx].Normal[1] = (sbyte)(n.Y * 127.0f);
                    MeshVertices[idx].Normal[2] = (sbyte)(n.Z * 127.0f);
                    MeshVertices[idx + 1].Normal[0] = (sbyte)(n.X * 127.0f);
                    MeshVertices[idx + 1].Normal[1] = (sbyte)(n.Y * 127.0f);
                    MeshVertices[idx + 1].Normal[2] = (sbyte)(n.Z * 127.0f);
                    MeshVertices[idx + 2].Normal[0] = (sbyte)(n.X * 127.0f);
                    MeshVertices[idx + 2].Normal[1] = (sbyte)(n.Y * 127.0f);
                    MeshVertices[idx + 2].Normal[2] = (sbyte)(n.Z * 127.0f);

                    MeshVertices[idx].Color[0] = (byte)(157);
                    MeshVertices[idx].Color[1] = (byte)(53);
                    MeshVertices[idx].Color[2] = (byte)(255);
                    MeshVertices[idx].Color[3] = (byte)(255);
                    MeshVertices[idx + 1].Color[0] = (byte)(157);
                    MeshVertices[idx + 1].Color[1] = (byte)(53);
                    MeshVertices[idx + 1].Color[2] = (byte)(255);
                    MeshVertices[idx + 1].Color[3] = (byte)(255);
                    MeshVertices[idx + 2].Color[0] = (byte)(157);
                    MeshVertices[idx + 2].Color[1] = (byte)(53);
                    MeshVertices[idx + 2].Color[2] = (byte)(255);
                    MeshVertices[idx + 2].Color[3] = (byte)(255);

                    MeshVertices[idx].Barycentric[0] = (byte)(0);
                    MeshVertices[idx].Barycentric[1] = (byte)(0);
                    MeshVertices[idx + 1].Barycentric[0] = (byte)(1);
                    MeshVertices[idx + 1].Barycentric[1] = (byte)(0);
                    MeshVertices[idx + 2].Barycentric[0] = (byte)(0);
                    MeshVertices[idx + 2].Barycentric[1] = (byte)(1);

                    MeshIndices[idx] = idx;
                    MeshIndices[idx + 1] = idx + 1;
                    MeshIndices[idx + 2] = idx + 2;
                    PickingIndices[idx] = idx;
                    PickingIndices[idx + 1] = idx + 1;
                    PickingIndices[idx + 2] = idx + 2;
                }
            }

            VertexCount = MeshVertices.Length;
            IndexCount = MeshIndices.Length;

            uint buffersize = (uint)IndexCount * 4u;

            if (VertexCount > 0)
            {
                fixed (void* ptr = PickingVertices)
                {
                    Bounds = BoundingBox.CreateFromPoints((Vector3*)ptr, PickingVertices.Count(), 12, Quaternion.Identity, Vector3.Zero, Vector3.One);
                }
            }
            else
            {
                Bounds = new BoundingBox();
            }

            uint vbuffersize = (uint)MeshVertices.Length * NavmeshLayout.SizeInBytes;

            GeomBuffer = Scene.Renderer.GeometryBufferAllocator.Allocate(vbuffersize, buffersize, (int)NavmeshLayout.SizeInBytes, 4, (h) =>
            {
                h.FillIBuffer(MeshIndices, () =>
                {
                    MeshIndices = null;
                });
                h.FillVBuffer(MeshVertices, () =>
                {
                    MeshVertices = null;
                });
            });
        }

        private bool LoadInternal(AccessLevel al)
        {
            if (al == AccessLevel.AccessFull || al == AccessLevel.AccessGPUOptimizedOnly)
            {
                Bounds = new BoundingBox();
                var mesh = (hkaiNavMesh)HkxRoot.m_namedVariants[0].m_variant;
                ProcessMesh(mesh);
            }

            if (al == AccessLevel.AccessGPUOptimizedOnly)
            {
                HkxRoot = null;
            }
            return true;
        }

        bool IResource._Load(byte[] bytes, AccessLevel al, GameType type)
        {
            BinaryReaderEx br = new BinaryReaderEx(false, bytes);
            var des = new HKX2.PackFileDeserializer();
            HkxRoot = (hkRootLevelContainer)des.Deserialize(br);
            return LoadInternal(al);
        }

        bool IResource._Load(string file, AccessLevel al, GameType type)
        {
            using (var s = File.OpenRead(file))
            {
                var des = new HKX2.PackFileDeserializer();
                HkxRoot = (hkRootLevelContainer)des.Deserialize(new BinaryReaderEx(false, s));
            }
            return LoadInternal(al);
        }

        public bool RayCast(Ray ray, Matrix4x4 transform, out float dist)
        {
            bool hit = false;
            float mindist = float.MaxValue;
            var invw = transform.Inverse();
            var newo = Vector3.Transform(ray.Origin, invw);
            var newd = Vector3.TransformNormal(ray.Direction, invw);
            var tray = new Ray(newo, newd);
            if (!tray.Intersects(Bounds))
            {
                dist = float.MaxValue;
                return false;
            }
            for (int index = 0; index < PickingIndices.Count(); index += 3)
            {
                float locdist;
                if (tray.Intersects(ref PickingVertices[PickingIndices[index]],
                    ref PickingVertices[PickingIndices[index + 1]],
                    ref PickingVertices[PickingIndices[index + 2]],
                    out locdist))
                {
                    hit = true;
                    if (locdist < mindist)
                    {
                        mindist = locdist;
                    }
                }
            }
            dist = mindist;
            return hit;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }

                GeomBuffer.Dispose();

                disposedValue = true;
            }
        }

        ~HavokNavmeshResource()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}