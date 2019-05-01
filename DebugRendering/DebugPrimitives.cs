using System;

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

            if (uvSplits != 0 && tesselations % uvSplits != 0) // FIXME: this can read a lot nicer i think?
            {
                throw new ArgumentException("expected the desired number of uv splits to be a divisor of the number of tesselations");
            }

            int hasUvSplits = (uvSplits > 0 ? 1 : 0);
            VertexPositionTexture[] vertices = new VertexPositionTexture[tesselations + (1 + (hasUvSplits + (hasUvSplits * uvSplits)))];
            int[] indices = new int[tesselations * 3 + 3 + (uvSplits * 3)];

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

            if (uvSplits != 0 && tesselations % uvSplits != 0) // FIXME: this can read a lot nicer i think?
            {
                throw new ArgumentException("expected the desired number of uv splits to be a divisor of the number of tesselations");
            }

            if (tesselations < 3) tesselations = 3;

            int verticalSegments = tesselations;
            int horizontalSegments = tesselations * 2;
            int hasUvSplit = (uvSplits > 0 ? 1 : 0);

            // FIXME: i tried figuring out a closed form solution for this bugger here, but i feel like i'm missing something crucial...
            //  it basically is just here to calculate how many extra vertices are needed to create the wireframe topology we want
            // if *you* can figure out a closed form solution, have at it! you are very welcome!
            int extraVertexCount = 0;

            if (hasUvSplit > 0)
            {
                for (int i = 0; i < verticalSegments; i++)
                {
                    for (int j = 0; j <= horizontalSegments; j++) {
                        int vertModulo = (i - 1) % (verticalSegments / uvSplits);
                        int horizModulo = (j - 1) % (horizontalSegments / uvSplits);
                        if (hasUvSplit > 0 && (vertModulo == 0 && horizModulo == 0)) {
                            extraVertexCount += 4;
                        } else if (hasUvSplit > 0 && (vertModulo == 0 || horizModulo == 0)) {
                            extraVertexCount += 2;
                        }
                    }
                }
            }

            var vertices = new VertexPositionTexture[(verticalSegments + 1) * (horizontalSegments + 1) + extraVertexCount];
            var indices = new int[(verticalSegments) * (horizontalSegments + 1) * 6];

            int vertexCount = 0;

            // generate the first extremity points
            for (int j = 0; j <= horizontalSegments; j++)
            {
                var normal = new Vector3(0, -1, 0);
                var textureCoordinate = new Vector2(0.5f);
                vertices[vertexCount++] = new VertexPositionTexture(normal * radius, textureCoordinate);
            }

            // Create rings of vertices at progressively higher latitudes.
            for (int i = 1; i < verticalSegments; i++)
            {

                var latitude = (float)((i * Math.PI / verticalSegments) - Math.PI / 2.0);
                var dy = (float)Math.Sin(latitude);
                var dxz = (float)Math.Cos(latitude);

                // the first point
                var firstNormal = new Vector3(0, dy, dxz);
                var firstHorizontalVertex = new VertexPositionTexture(firstNormal * radius, new Vector2(0.5f));
                vertices[vertexCount++] = firstHorizontalVertex;

                // Create a single ring of vertices at this latitude.
                for (int j = 1; j < horizontalSegments; j++)
                {

                    var longitude = (float)(j * 2.0 * Math.PI / horizontalSegments);
                    var dx = (float)Math.Sin(longitude);
                    var dz = (float)Math.Cos(longitude);

                    dx *= dxz;
                    dz *= dxz;

                    var normal = new Vector3(dx, dy, dz);
                    var textureCoordinate = new Vector2(0.5f);

                    vertices[vertexCount++] = new VertexPositionTexture(normal * radius, textureCoordinate);
                }

                // the last point equal to the first point
                firstHorizontalVertex.TextureCoordinate = new Vector2(0.5f);
                vertices[vertexCount++] = firstHorizontalVertex;
            }

            // generate the end extremity points
            for (int j = 0; j <= horizontalSegments; j++)
            {
                var normal = new Vector3(0, 1, 0);
                var textureCoordinate = new Vector2(0.5f);
                vertices[vertexCount++] = new VertexPositionTexture(normal * radius, textureCoordinate);
            }

            // Fill the index buffer with triangles joining each pair of latitude rings.
            int stride = horizontalSegments + 1;

            int indexCount = 0;
            int newVertexCount = vertexCount;
            for (int i = 0; i < verticalSegments; i++)
            {
                for (int j = 0; j <= horizontalSegments; j++)
                {
                    int nextI = i + 1;
                    int nextJ = (j + 1) % stride;
                    int vertModulo = (i - 1) % (verticalSegments / uvSplits);
                    int horizModulo = (j - 1) % (horizontalSegments / uvSplits);
                    if (hasUvSplit > 0 && (vertModulo == 0 && horizModulo == 0))
                    {

                        vertices[newVertexCount] = vertices[(i * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (i * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        indices[indexCount++] = (i * stride + nextJ);


                        indices[indexCount++] = (i * stride + nextJ);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + nextJ)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + nextJ);

                    }
                    else if (hasUvSplit > 0 && vertModulo == 0)
                    {

                        indices[indexCount++] = (i * stride + j);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (i * stride + nextJ);

                        indices[indexCount++] = (i * stride + nextJ);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + nextJ)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + nextJ);

                    }
                    else if (hasUvSplit > 0 && horizModulo == 0)
                    {

                        vertices[newVertexCount] = vertices[(i * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (i * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        indices[indexCount++] = (i * stride + nextJ);


                        indices[indexCount++] = (i * stride + nextJ);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (nextI * stride + nextJ);

                    }
                    else
                    {

                        indices[indexCount++] = (i * stride + j);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (i * stride + nextJ);

                        indices[indexCount++] = (i * stride + nextJ);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (nextI * stride + nextJ);

                    }

                }
            }

            return (vertices, indices);

        }

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCylinder(float height = 1.0f, float radius = 0.5f, int tesselations = 16, int uvSides = 4, int? uvSidesForCircle = null)
        {

            if (uvSides != 0 && tesselations % uvSides != 0) // FIXME: this can read a lot nicer i think?
            {
                throw new ArgumentException("expected the desired number of uv splits to be a divisor of the number of tesselations");
            }

            var hasUvSplit = (uvSides > 0 ? 1 : 0);
            var (capVertices, capIndices) = GenerateCircle(radius, tesselations, uvSidesForCircle ?? uvSides);



            VertexPositionTexture[] vertices = new VertexPositionTexture[capVertices.Length * 2 + (tesselations * 2) + ((tesselations / uvSides)+2) * 4];
            int[] indices = new int[capIndices.Length * 2 + (tesselations*6)];

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

            if (uvSplits != 0 && tesselations % uvSplits != 0) // FIXME: this can read a lot nicer i think?
            {
                throw new ArgumentException("expected the desired number of uv splits to be a divisor of the number of tesselations");
            }

            if (uvSplitsBottom != 0 && tesselations % uvSplitsBottom != 0) // FIXME: this can read a lot nicer i think?
            {
                throw new ArgumentException("expected the desired number of uv splits for the bottom to be a divisor of the number of tesselations");
            }

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

        public static(VertexPositionTexture[] Vertices, int[] Indices) GenerateCapsule(float length, float radius, int tesselation, int uvSplits = 4)
        {

            if (uvSplits != 0 && tesselation % uvSplits != 0) // FIXME: this can read a lot nicer i think?
            {
                throw new ArgumentException("expected the desired number of uv splits to be a divisor of the number of tesselations");
            }

            if (tesselation < 3) tesselation = 3;

            int verticalSegments = 2 * tesselation;
            int horizontalSegments = 4 * tesselation;
            int hasUvSplit = (uvSplits > 0) ? 1 : 0;

            // FIXME: i tried figuring out a closed form solution for this bugger here, but i feel like i'm missing something crucial...
            //  it basically is just here to calculate how many extra vertices are needed to create the wireframe topology we want
            // if *you* can figure out a closed form solution, have at it! you are very welcome!
            int extraVertexCount = 0;

            if (hasUvSplit > 0)
            {
                for (int i = 0; i < verticalSegments - 1; i++) 
                {
                    for (int j = 0; j <= horizontalSegments; j++)
                    {
                        int vertModulo = (i - 1) % (verticalSegments / uvSplits);
                        int horizModulo = (j - 1) % (horizontalSegments / uvSplits);
                        if (hasUvSplit > 0 && (vertModulo == 0 && horizModulo == 0))
                        {
                            extraVertexCount += 4;
                        } else if (hasUvSplit > 0 && (vertModulo == 0 || horizModulo == 0))
                        {
                            extraVertexCount += 2;
                        }
                    }
                }
            }

            var vertices = new VertexPositionTexture[verticalSegments * (horizontalSegments + 1) + extraVertexCount];
            var indices = new int[(verticalSegments - 1) * (horizontalSegments + 1) * 6];
            
            var vertexCount = 0;
            // Create rings of vertices at progressively higher latitudes.
            for (int i = 0; i < verticalSegments; i++) {
                float deltaY;
                float latitude;

                if (i < verticalSegments / 2) {
                    deltaY = -length / 2;
                    latitude = (float)((i * Math.PI / (verticalSegments - 2)) - Math.PI / 2.0);
                } else {
                    deltaY = length / 2;
                    latitude = (float)(((i - 1) * Math.PI / (verticalSegments - 2)) - Math.PI / 2.0);
                }

                var dy = (float)Math.Sin(latitude);
                var dxz = (float)Math.Cos(latitude);

                // Create a single ring of vertices at this latitude.
                for (int j = 0; j <= horizontalSegments; j++) {

                    var longitude = (float)(j * 2.0 * Math.PI / horizontalSegments);
                    var dx = (float)Math.Sin(longitude);
                    var dz = (float)Math.Cos(longitude);

                    dx *= dxz;
                    dz *= dxz;

                    var normal = new Vector3(dx, dy, dz);
                    var textureCoordinate = new Vector2(0.5f);
                    var position = radius * normal + new Vector3(0, deltaY, 0);

                    vertices[vertexCount++] = new VertexPositionTexture(position, textureCoordinate);
                }
            }

            // Fill the index buffer with triangles joining each pair of latitude rings.
            int stride = horizontalSegments + 1;

            int indexCount = 0;
            int newVertexCount = vertexCount;
            for (int i = 0; i < verticalSegments - 1; i++)
            {
                for (int j = 0; j <= horizontalSegments; j++)
                {
                    int nextI = i + 1;
                    int nextJ = (j + 1) % stride;
                    int vertModulo = (i - 1) % (verticalSegments / uvSplits);
                    int horizModulo = (j - 1) % (horizontalSegments / uvSplits);
                    if (hasUvSplit > 0 && (vertModulo == 0 && horizModulo == 0))
                    {

                        vertices[newVertexCount] = vertices[(i * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (i * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        indices[indexCount++] = (i * stride + nextJ);


                        indices[indexCount++] = (i * stride + nextJ);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + nextJ)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + nextJ);

                    }
                    else if (hasUvSplit > 0 && vertModulo == 0)
                    {

                        indices[indexCount++] = (i * stride + j);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (i * stride + nextJ);

                        indices[indexCount++] = (i * stride + nextJ);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + nextJ)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + nextJ);

                    }
                    else if (hasUvSplit > 0 && horizModulo == 0)
                    {

                        vertices[newVertexCount] = vertices[(i * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (i * stride + j);

                        vertices[newVertexCount] = vertices[(nextI * stride + j)];
                        vertices[newVertexCount].TextureCoordinate = new Vector2(1.0f);
                        indices[indexCount++] = newVertexCount++; // indices[indexCount++] = (nextI * stride + j);

                        indices[indexCount++] = (i * stride + nextJ);


                        indices[indexCount++] = (i * stride + nextJ);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (nextI * stride + nextJ);

                    }
                    else
                    {

                        indices[indexCount++] = (i * stride + j);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (i * stride + nextJ);

                        indices[indexCount++] = (i * stride + nextJ);
                        indices[indexCount++] = (nextI * stride + j);
                        indices[indexCount++] = (nextI * stride + nextJ);

                    }

                }
            }

            return (vertices, indices);

        }

    }
}
