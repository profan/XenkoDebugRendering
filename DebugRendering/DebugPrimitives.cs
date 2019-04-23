﻿using System;

using Xenko.Core.Mathematics;
using Xenko.Graphics.GeometricPrimitives;
using Xenko.Graphics;

namespace DebugRendering {
    public class DebugPrimitives {

        public static void CopyFromGeometricPrimitive(GeometricMeshData<VertexPositionNormalTexture> primitiveData, ref VertexPositionTexture[] vertices, ref int[] indices)
        {
            for (int i = 0; i < vertices.Length; ++i)
            {
                vertices[i].Position = primitiveData.Vertices[i].Position;
                vertices[i].TextureCoordinate = primitiveData.Vertices[i].TextureCoordinate;
            }

            for (int i = 0; i < indices.Length; ++i)
            {
                indices[i] = primitiveData.Indices[i];
            }
        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateQuad(float width, float height)
        {

            var quadMeshData = GeometricPrimitive.Plane.New(width, height);
            VertexPositionTexture[] vertices = new VertexPositionTexture[quadMeshData.Vertices.Length];
            int[] indices = new int[quadMeshData.Indices.Length];

            CopyFromGeometricPrimitive(quadMeshData, ref vertices, ref indices);

            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCircle(float radius = 0.5f, int tesselations = 16, int uvSplits = 0, float yOffset = 0.0f)
        {

            int hasUvSplits = (uvSplits > 0 ? 1 : 0);
            VertexPositionTexture[] vertices = new VertexPositionTexture[tesselations + (1 + (hasUvSplits + (hasUvSplits * uvSplits)))];
            int[] indices = new int[tesselations * 3 + 3 + uvSplits * 3];

            double radiansPerSegment = MathUtil.TwoPi / tesselations;

            // center of our circle
            vertices[0].Position = new Vector3(0.0f, yOffset, 0.0f);
            vertices[0].TextureCoordinate = new Vector2(0.5f);

            // center, but with uv coords set
            if (hasUvSplits > 0)
            {
                vertices[1].Position = new Vector3(0.0f, yOffset, 0.0f);
                vertices[1].TextureCoordinate = new Vector2(1.0f);
            }

            // in the XZ plane
            float curX = 0.0f, curZ = 0.0f;
            for (int i = 1 + hasUvSplits; i < tesselations + (1 + hasUvSplits); ++i)
            {
                curX = (float)(Math.Cos(i * radiansPerSegment) * (2.0f * radius)) / 2.0f;
                curZ = (float)(Math.Sin(i * radiansPerSegment) * (2.0f * radius)) / 2.0f;
                vertices[i].Position = new Vector3(curX, yOffset, curZ);
                vertices[i].TextureCoordinate = new Vector2(1.0f);
            }

            int curVert = 1 + hasUvSplits;
            int lastIndex = 0;
            for (int i = 0; i < tesselations * 3 - (3 * hasUvSplits); i += 3)
            {
                indices[i] = 0;
                indices[i + 1] = curVert;
                indices[i + 2] = curVert + 1;
                lastIndex = i;
                curVert++;
            }

            // endpoint
            indices[lastIndex + 3] = 0;
            indices[lastIndex + 4] = indices[lastIndex + 1 + hasUvSplits];
            indices[lastIndex + 5] = indices[1];
            lastIndex = lastIndex + 5;

            // draw uv lines
            int newVert = tesselations + (1 + hasUvSplits);
            int curNewIndex = lastIndex + 1;
            for (int v = 1 + hasUvSplits; v < tesselations + (1 + hasUvSplits); ++v)
            {
                if (hasUvSplits > 0)
                {
                    var splitMod = (v - 1) % (tesselations / uvSplits);
                    var timeToSplit = (splitMod == 0);
                    if (timeToSplit)
                    {
                        indices[curNewIndex] = 1;
                        indices[curNewIndex + 1] = v;
                        vertices[newVert] = vertices[v + 1];
                        vertices[newVert].TextureCoordinate = new Vector2(0.5f);
                        indices[curNewIndex + 2] = newVert++;
                        curNewIndex += 3;
                    }
                }
            }

            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCube(float size = 1.0f)
        {

            var cubeMeshData = GeometricPrimitive.Cube.New(size);
            VertexPositionTexture[] vertices = new VertexPositionTexture[cubeMeshData.Vertices.Length];
            int[] indices = new int[cubeMeshData.Indices.Length];

            CopyFromGeometricPrimitive(cubeMeshData, ref vertices, ref indices);

            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateSphere(float radius = 0.5f, int tesselations = 16, int uvSplits = 4)
        {

            VertexPositionTexture[] vertices = new VertexPositionTexture[1];
            int[] indices = new int[1];



            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCylinder(float height = 1.0f, float radius = 0.5f, int tesselations = 16, int uvSides = 4, int? uvSidesForCircle = null) {

            var hasUvSplit = (uvSides > 0 ? 1 : 0);
            var (capVertices, capIndices) = GenerateCircle(radius, tesselations, uvSidesForCircle ?? uvSides);

            VertexPositionTexture[] vertices = new VertexPositionTexture[capVertices.Length * 2 + tesselations * 4];
            int[] indices = new int[capIndices.Length * 2 + tesselations * 6];

            // copy vertices
            for (int i = 0; i < capVertices.Length; ++i)
            {
                vertices[i] = capVertices[i];
                vertices[i + capVertices.Length] = capVertices[i];
                vertices[i + capVertices.Length].Position.Y = height;
            }

            // copy indices
            for (int i = 0; i < capIndices.Length; ++i) 
            {
                indices[i] = capIndices[i];
                indices[i + capIndices.Length] = capIndices[i] + capVertices.Length;
            }

            // generate sides, using our top and bottom circle triangle fans
            int curVert = capVertices.Length * 2;
            int curIndex = capIndices.Length * 2;
            for (int i = 1 + hasUvSplit; i < capVertices.Length - (uvSides * hasUvSplit); ++i)
            {
                int sideModulo = (i - 1 - hasUvSplit) % (tesselations / uvSides);
                if (sideModulo == 0)
                {

                    vertices[curVert] = vertices[i];
                    vertices[curVert].TextureCoordinate = new Vector2(0.5f);
                    var ip = curVert++;

                    vertices[curVert] = vertices[i + capVertices.Length];
                    vertices[curVert].TextureCoordinate = new Vector2(0.5f);
                    var ipv = curVert++;

                    // make some fresh vertex shit yo
                    indices[curIndex++] = ip;
                    indices[curIndex++] = i + 1;
                    indices[curIndex++] = ipv;

                    indices[curIndex++] = ipv;
                    indices[curIndex++] = i + capVertices.Length + 1;
                    indices[curIndex++] = i + 1;

                } else
                {

                    vertices[curVert] = vertices[i];
                    vertices[curVert].TextureCoordinate = new Vector2(0.5f);
                    var ip = curVert++;

                    vertices[curVert] = vertices[i + 1];
                    vertices[curVert].TextureCoordinate = new Vector2(0.5f);
                    var ip1 = curVert++;

                    vertices[curVert] = vertices[i + capVertices.Length];
                    vertices[curVert].TextureCoordinate = new Vector2(0.5f);
                    var ipv = curVert++;

                    vertices[curVert] = vertices[i + 1 + capVertices.Length];
                    vertices[curVert].TextureCoordinate = new Vector2(0.5f);
                    var ipv1 = curVert++;

                    // reuse the old stuff yo
                    indices[curIndex++] = ip;
                    indices[curIndex++] = ip1;
                    indices[curIndex++] = ipv;

                    indices[curIndex++] = ipv;
                    indices[curIndex++] = ipv1;
                    indices[curIndex++] = ip1;

                }
            }

            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCone(float height, float radius, int tesselations, int uvSplits = 4, int uvSplitsBottom = 0)
        {

            var (bottomVertices, bottomIndices) = GenerateCircle(radius, tesselations, uvSplits);
            var (topVertices, topIndices) = GenerateCircle(radius, tesselations, uvSplitsBottom);
            VertexPositionTexture[] vertices = new VertexPositionTexture[bottomVertices.Length * 2];
            int[] indices = new int[bottomIndices.Length * 2];

            // copy vertices from circle
            for (int i = 0; i < bottomVertices.Length; ++i)
            {
                vertices[i] = bottomVertices[i];
            }

            for (int i = 0; i < topVertices.Length; ++i)
            {
                vertices[i + bottomVertices.Length] = topVertices[i];
            }

            // copy indices from circle
            for (int i = 0; i < bottomIndices.Length; ++i)
            {
                indices[i] = bottomIndices[i];
            }

            for (int i = 0; i < topIndices.Length; ++i)
            {
                indices[i + bottomIndices.Length] = topIndices[i] + bottomVertices.Length;
            }

            // extrude middle vertex of center of first circle triangle fan
            vertices[0].Position.Y = height;
            vertices[1].Position.Y = height;

            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCapsule(float height, float radius, int tesselation, int uvSplits = 4)
        {

            var (capVertices, capIndices) = GenerateCircle(radius, tesselation, 4, yOffset: height);
            var (midVertices, midIndices) = GenerateCylinder(height, radius, tesselation, uvSides: uvSplits);

            VertexPositionTexture[] vertices = new VertexPositionTexture[capVertices.Length + midVertices.Length];
            int[] indices = new int[capIndices.Length + midIndices.Length];

            /*
            var capsuleData = GeometricPrimitive.Capsule.New(height, radius, tesselation);
            VertexPositionTexture[] vertices = new VertexPositionTexture[capsuleData.Vertices.Length];
            int[] indices = new int[capsuleData.Indices.Length];
            CopyFromGeometricPrimitive(capsuleData, ref vertices, ref indices);
            */

            return (vertices, indices);

        }

    }
}
