// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Core.Threading;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Rendering;

using Buffer = Xenko.Graphics.Buffer;

namespace DebugRendering
{

    public class DebugSystem : GameSystemBase
    {

        public enum RenderingMode
        {
            Wireframe,
            Solid
        }

        internal enum DebugRenderableType : byte
        {
            Quad,
            QuadNoDepth,
            Circle,
            CircleNoDepth,
            Line,
            LineNoDepth,
            Cube,
            CubeNoDepth,
            Sphere,
            SphereNoDepth,
            Capsule,
            CapsuleNoDepth,
            Cylinder,
            CylinderNoDepth,
            Cone,
            ConeNoDepth
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct DebugRenderable
        {

            public DebugRenderable(ref DebugDrawQuad q, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Quad : DebugRenderableType.QuadNoDepth;
                QuadData = q;
            }

            public DebugRenderable(ref DebugDrawCircle c, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Circle : DebugRenderableType.CircleNoDepth;
                CircleData = c;
            }

            public DebugRenderable(ref DebugDrawLine l, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Line : DebugRenderableType.LineNoDepth;
                LineData = l;
            }

            public DebugRenderable(ref DebugDrawCube b, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Cube : DebugRenderableType.CubeNoDepth;
                CubeData = b;
            }

            public DebugRenderable(ref DebugDrawSphere s, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Sphere : DebugRenderableType.SphereNoDepth;
                SphereData = s;
            }

            public DebugRenderable(ref DebugDrawCapsule c, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Capsule : DebugRenderableType.CapsuleNoDepth;
                CapsuleData = c;
            }

            public DebugRenderable(ref DebugDrawCylinder c, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Cylinder : DebugRenderableType.CylinderNoDepth;
                CylinderData = c;
            }

            public DebugRenderable(ref DebugDrawCone c, bool depthTest) : this()
            {
                Type = (depthTest) ? DebugRenderableType.Cone : DebugRenderableType.ConeNoDepth;
                ConeData = c;
            }

            [FieldOffset(0)]
            public DebugRenderableType Type;

            [FieldOffset(1)]
            public float Lifetime;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawQuad QuadData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawCircle CircleData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawLine LineData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawCube CubeData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawSphere SphereData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawCapsule CapsuleData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawCylinder CylinderData;

            [FieldOffset(1 + sizeof(float))]
            public DebugDrawCone ConeData;

        }

        internal struct DebugDrawQuad
        {
            public Vector3 Position;
            public Vector2 Size;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct DebugDrawCircle
        {
            public Vector3 Position;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct DebugDrawLine
        {
            public Vector3 Start;
            public Vector3 End;
            public Color Color;
        }

        internal struct DebugDrawCube
        {
            public Vector3 Position;
            public Vector3 End;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct DebugDrawSphere
        {
            public Vector3 Position;
            public float Radius;
            public Color Color;
        }

        internal struct DebugDrawCapsule
        {
            public Vector3 Position;
            public float Height;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct DebugDrawCylinder
        {
            public Vector3 Position;
            public float Height;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct DebugDrawCone
        {
            public Vector3 Position;
            public float Height;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        private readonly FastList<DebugRenderable> renderMessages = new FastList<DebugRenderable>();
        private readonly FastList<DebugRenderable> renderMessagesWithLifetime = new FastList<DebugRenderable>();

        /* FIXME: this is set from outside atm, bit of a hack */
        public DebugRenderFeature PrimitiveRenderer;

        public RenderingMode RenderMode { get; set; } = RenderingMode.Wireframe;

        public Color PrimitiveColor { get; set; } = Color.LightGreen;

        public int MaxPrimitives { get; set; } = 100;
        public int MaxPrimitivesWithLifetime { get; set; } = 100;

        public DebugSystem(IServiceRegistry registry) : base(registry)
        {
            Enabled = true;
            Visible = Platform.IsRunningDebugAssembly;

            DrawOrder = 0xffffff;
            UpdateOrder = -100100; //before script
        }

        private void PushMessage(ref DebugRenderable msg)
        {
            if (msg.Lifetime > 0.0f)
            {
                renderMessagesWithLifetime.Add(msg);
                // drop one old message if the tail size has been reached
                if (renderMessagesWithLifetime.Count > MaxPrimitivesWithLifetime)
                {
                    renderMessagesWithLifetime.RemoveAt(renderMessagesWithLifetime.Count - 1);
                }
            }
            else
            {
                renderMessages.Add(msg);
                // drop one old message if the tail size has been reached
                if (renderMessages.Count > MaxPrimitives)
                {
                    renderMessages.RemoveAt(renderMessages.Count - 1);
                }
            }
        }

        public void DrawLine(Vector3 start, Vector3 end, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawLine { Start = start, End = end, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawLines(Vector3[] vertices, Color? color = null, float duration = 0.0f, bool depthTest = true)
        {
            var totalVertexPairs = vertices.Length - (vertices.Length % 2);
            for (int i = 0; i < totalVertexPairs; i += 2)
            {
                ref var v1 = ref vertices[i];
                ref var v2 = ref vertices[i];
                DrawLine(v1, v2, color ?? PrimitiveColor, duration, depthTest);
            }
        }

        public void DrawRay(Vector3 start, Vector3 dir, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            DrawLine(start, start + dir, color == default ? PrimitiveColor : color, duration, depthTest);
        }

        public void DrawArrow(Vector3 from, Vector3 dir, float coneHeight = 1.0f, float coneRadius = 0.5f, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            DrawRay(from, dir, color, duration, depthTest);
            DrawCone(from + dir, coneHeight, coneRadius, Quaternion.BetweenDirections(new Vector3(0.0f, 1.0f, 0.0f), dir), color == default ? PrimitiveColor : color, duration, depthTest);
        }

        public void DrawSphere(Vector3 position, float radius, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawSphere { Position = position, Radius = radius, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawBounds(Vector3 start, Vector3 end, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCube { Position = start + ((end - start) / 2), End = end + ((end - start) / 2), Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCube(Vector3 start, Vector3 size, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCube { Position = start, End = start + size, Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCapsule(Vector3 position, float height, float radius, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCapsule { Position = position, Height = height, Radius = radius, Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCylinder(Vector3 position, float height, float radius, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCylinder { Position = position, Height = height, Radius = radius, Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCone(Vector3 position, float height, float radius, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCone { Position = position, Height = height, Radius = radius, Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawQuad(Vector3 position, Vector2 size, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawQuad { Position = position, Size = size, Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCircle(Vector3 position, float radius, Quaternion rotation = default, Color color = default, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCircle { Position = position, Radius = radius, Rotation = rotation == default ? Quaternion.Identity : rotation, Color = color == default ? PrimitiveColor : color };
            var msg = new DebugRenderable(ref cmd, depthTest) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public override void Update(GameTime gameTime)
        {

            switch (RenderMode) {
                case RenderingMode.Wireframe:
                    PrimitiveRenderer.SetFillMode(FillMode.Wireframe);
                    break;
                case RenderingMode.Solid:
                    PrimitiveRenderer.SetFillMode(FillMode.Solid);
                    break;
            }

            HandlePrimitives(gameTime, renderMessages);
            HandlePrimitives(gameTime, renderMessagesWithLifetime);

            float delta = (float)gameTime.Elapsed.TotalSeconds;

            /* clear out any messages with no lifetime left */
            for (int i = 0; i < renderMessagesWithLifetime.Count; ++i)
            {
                renderMessagesWithLifetime.Items[i].Lifetime -= delta;
            }

            renderMessagesWithLifetime.RemoveAll((msg) => msg.Lifetime <= 0.0f);

            /* just clear our per-frame array */
            renderMessages.Clear(true);

        }

        private void HandlePrimitives(GameTime gameTime, FastList<DebugRenderable> messages)
        {

            if (messages.Count == 0)
            {
                return;
            }

            for (int i = 0; i < messages.Count; ++i)
            {
                ref var msg = ref messages.Items[i];
                switch (msg.Type)
                {
                    case DebugRenderableType.Quad:
                        PrimitiveRenderer.DrawQuad(ref msg.QuadData.Position, ref msg.QuadData.Size, ref msg.QuadData.Rotation, ref msg.QuadData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.QuadNoDepth:
                        PrimitiveRenderer.DrawQuad(ref msg.QuadData.Position, ref msg.QuadData.Size, ref msg.QuadData.Rotation, ref msg.QuadData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Circle:
                        PrimitiveRenderer.DrawCircle(ref msg.CircleData.Position, msg.CircleData.Radius, ref msg.CircleData.Rotation, ref msg.CircleData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CircleNoDepth:
                        PrimitiveRenderer.DrawCircle(ref msg.CircleData.Position, msg.CircleData.Radius, ref msg.CircleData.Rotation, ref msg.CircleData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Line:
                        PrimitiveRenderer.DrawLine(ref msg.LineData.Start, ref msg.LineData.End, ref msg.LineData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.LineNoDepth:
                        PrimitiveRenderer.DrawLine(ref msg.LineData.Start, ref msg.LineData.End, ref msg.LineData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Cube:
                        PrimitiveRenderer.DrawCube(ref msg.CubeData.Position, ref msg.CubeData.End, ref msg.CubeData.Rotation, ref msg.CubeData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CubeNoDepth:
                        PrimitiveRenderer.DrawCube(ref msg.CubeData.Position, ref msg.CubeData.End, ref msg.CubeData.Rotation, ref msg.CubeData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Sphere:
                        PrimitiveRenderer.DrawSphere(ref msg.SphereData.Position, msg.SphereData.Radius, ref msg.SphereData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.SphereNoDepth:
                        PrimitiveRenderer.DrawSphere(ref msg.SphereData.Position, msg.SphereData.Radius, ref msg.SphereData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Capsule:
                        PrimitiveRenderer.DrawCapsule(ref msg.CapsuleData.Position, msg.CapsuleData.Height, msg.CapsuleData.Radius, ref msg.CapsuleData.Rotation, ref msg.CapsuleData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CapsuleNoDepth:
                        PrimitiveRenderer.DrawCapsule(ref msg.CapsuleData.Position, msg.CapsuleData.Height, msg.CapsuleData.Radius, ref msg.CapsuleData.Rotation, ref msg.CapsuleData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Cylinder:
                        PrimitiveRenderer.DrawCylinder(ref msg.CylinderData.Position, msg.CylinderData.Height, msg.CylinderData.Radius, ref msg.CylinderData.Rotation, ref msg.CylinderData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CylinderNoDepth:
                        PrimitiveRenderer.DrawCylinder(ref msg.CylinderData.Position, msg.CylinderData.Height, msg.CylinderData.Radius, ref msg.CylinderData.Rotation, ref msg.CylinderData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Cone:
                        PrimitiveRenderer.DrawCone(ref msg.ConeData.Position, msg.ConeData.Height, msg.ConeData.Radius, ref msg.ConeData.Rotation, ref msg.ConeData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.ConeNoDepth:
                        PrimitiveRenderer.DrawCone(ref msg.ConeData.Position, msg.ConeData.Height, msg.ConeData.Radius, ref msg.ConeData.Rotation, ref msg.ConeData.Color, depthTest: false);
                        break;
                }
            }

        }

    }

    internal class DummyDebugRenderObject : RenderObject
    {

    }

    public class DebugRenderFeature : RootRenderFeature
    {

        public override Type SupportedRenderObjectType => typeof(DummyDebugRenderObject);

        internal struct Primitives
        {

            public int Quads;
            public int Circles;
            public int Spheres;
            public int Cubes;
            public int Capsules;
            public int Cylinders;
            public int Cones;
            public int Lines;

            public void Clear()
            {
                Quads = 0;
                Circles = 0;
                Spheres = 0;
                Cubes = 0;
                Capsules = 0;
                Cylinders = 0;
                Cones = 0;
                Lines = 0;
            }

        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        internal struct LineVertex
        {

            public static readonly VertexDeclaration Layout = new VertexDeclaration(VertexElement.Position<Vector3>(), VertexElement.Color<Color>());

            public Vector3 Position;
            public Color Color;

        }

        internal enum RenderableType : byte
        {
            Quad,
            Circle,
            Sphere,
            Cube,
            Capsule,
            Cylinder,
            Cone,
            Line
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct Renderable
        {

            public Renderable(ref Quad q) : this()
            {
                Type = RenderableType.Quad;
                QuadData = q;
            }

            public Renderable(ref Circle c) : this()
            {
                Type = RenderableType.Circle;
                CircleData = c;
            }

            public Renderable(ref Sphere s) : this()
            {
                Type = RenderableType.Sphere;
                SphereData = s;
            }

            public Renderable(ref Cube c) : this()
            {
                Type = RenderableType.Cube;
                CubeData = c;
            }

            public Renderable(ref Capsule c) : this()
            {
                Type = RenderableType.Capsule;
                CapsuleData = c;
            }

            public Renderable(ref Cylinder c) : this()
            {
                Type = RenderableType.Cylinder;
                CylinderData = c;
            }

            public Renderable(ref Cone c) : this()
            {
                Type = RenderableType.Cone;
                ConeData = c;
            }

            public Renderable(ref Line l) : this()
            {
                Type = RenderableType.Line;
                LineData = l;
            }

            [FieldOffset(0)]
            public RenderableType Type;

            [FieldOffset(1)]
            public Quad QuadData;

            [FieldOffset(1)]
            public Circle CircleData;

            [FieldOffset(1)]
            public Sphere SphereData;

            [FieldOffset(1)]
            public Cube CubeData;

            [FieldOffset(1)]
            public Capsule CapsuleData;

            [FieldOffset(1)]
            public Cylinder CylinderData;

            [FieldOffset(1)]
            public Cone ConeData;

            [FieldOffset(1)]
            public Line LineData;

        }

        internal struct Quad
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector2 Size;
            public Color Color;
        }

        internal struct Circle
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Radius;
            public Color Color;
        }

        internal struct Sphere
        {
            public Vector3 Position;
            public float Radius;
            public Color Color;
        }

        internal struct Cube
        {
            public Vector3 Start;
            public Vector3 End;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct Capsule
        {
            public Vector3 Position;
            public float Height;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct Cylinder
        {
            public Vector3 Position;
            public float Height;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct Cone
        {
            public Vector3 Position;
            public float Height;
            public float Radius;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct Line
        {
            public Vector3 Start;
            public Vector3 End;
            public Color Color;
        }

        internal struct InstanceData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Color Color;
        }

        private const float DEFAULT_SPHERE_RADIUS = 0.5f;
        private const float DEFAULT_CUBE_SIZE = 1.0f;
        private const float DEFAULT_CAPSULE_LENGTH = 1.0f;
        private const float DEFAULT_CAPSULE_RADIUS = 0.5f;
        private const float DEFAULT_CYLINDER_HEIGHT = 1.0f;
        private const float DEFAULT_CYLINDER_RADIUS = 0.5f;
        private const float DEFAULT_PLANE_SIZE = 1.0f;
        private const float DEFAULT_CONE_RADIUS = 0.5f;
        private const float DEFAULT_CONE_HEIGHT = 1.0f;

        private const int CIRCLE_TESSELATION = 16;
        private const int SPHERE_TESSELATION = 8;
        private const int CAPSULE_TESSELATION = 8;
        private const int CYLINDER_TESSELATION = 16;
        private const int CONE_TESSELATION = 16;

        /* mesh data we will use when stuffing things in vertex buffers */
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) circle = DebugPrimitives.GenerateCircle(0.5f, CIRCLE_TESSELATION, 0);
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) plane = DebugPrimitives.GenerateQuad(DEFAULT_PLANE_SIZE, DEFAULT_PLANE_SIZE);
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) sphere = DebugPrimitives.GenerateSphere(DEFAULT_SPHERE_RADIUS, SPHERE_TESSELATION);
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) cube = DebugPrimitives.GenerateCube(DEFAULT_CUBE_SIZE);
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) capsule = DebugPrimitives.GenerateCapsule(DEFAULT_CAPSULE_LENGTH, DEFAULT_CAPSULE_RADIUS, CAPSULE_TESSELATION);
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) cylinder = DebugPrimitives.GenerateCylinder(DEFAULT_CYLINDER_HEIGHT, DEFAULT_CYLINDER_RADIUS, CYLINDER_TESSELATION);
        private readonly (VertexPositionTexture[] Vertices, int[] Indices) cone = DebugPrimitives.GenerateCone(DEFAULT_CONE_HEIGHT, DEFAULT_CONE_RADIUS, CONE_TESSELATION, uvSplits: 8);

        /* vertex and index buffer for our primitive data */
        private Buffer vertexBuffer;
        private Buffer indexBuffer;

        /* vertex buffer for line rendering */
        private Buffer lineVertexBuffer;

        /* offsets into our vertex/index buffer */
        private Primitives primitiveVertexOffsets;
        private Primitives primitiveIndexOffsets;

        /* other gpu related data */
        private MutablePipelineState pipelineState;
        private InputElementDescription[] inputElements;
        private InputElementDescription[] lineInputElements;
        private EffectInstance primitiveEffect;
        private EffectInstance lineEffect;
        private Buffer transformBuffer;
        private Buffer colorBuffer;

        /* messages */
        private readonly FastList<Renderable> renderablesWithDepth = new FastList<Renderable>();
        private readonly FastList<Renderable> renderablesNoDepth = new FastList<Renderable>();

        /* accumulators used when data is being pushed to the system */
        private Primitives totalPrimitives, totalPrimitivesNoDepth;
        
        /* used to specify offset into instance data buffers when drawing */
        private Primitives instanceOffsets, instanceOffsetsNoDepth;

        /* used in render stage to know how many of each instance to draw */
        private Primitives primitivesToDraw, primitivesToDrawNoDepth;

        /* intermediate message related data, written to in extract */
        private readonly FastList<InstanceData> instances = new FastList<InstanceData>(1);

        /* data written to buffers in prepare */
        private readonly FastList<Matrix> transforms = new FastList<Matrix>(1);
        private readonly FastList<Color> colors = new FastList<Color>(1);

        /* data only for line rendering */
        private readonly FastList<LineVertex> lineVertices = new FastList<LineVertex>(1);

        /* state set from outside */
        private FillMode currentFillMode = FillMode.Wireframe;

        public DebugRenderFeature()
        {
        }

        public void SetFillMode(FillMode fillMode)
        {
            currentFillMode = fillMode;
        }

        public void DrawQuad(ref Vector3 position, ref Vector2 size, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Quad() { Position = position, Size = size, Rotation = rotation, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Quads++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Quads++;
            }
        }

        public void DrawCircle(ref Vector3 position, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Circle() { Position = position, Radius = radius, Rotation = rotation, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Circles++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Circles++;
            }
        }

        public void DrawSphere(ref Vector3 position, float radius, ref Color color, bool depthTest = true)
        {
            var cmd = new Sphere() { Position = position, Radius = radius, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Spheres++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Spheres++;
            }
        }

        public void DrawCube(ref Vector3 start, ref Vector3 end, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Cube() { Start = start, End = end, Rotation = rotation, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Cubes++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Cubes++;
            }
        }

        public void DrawCapsule(ref Vector3 position, float height, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Capsule() { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Capsules++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Capsules++;
            }
        }

        public void DrawCylinder(ref Vector3 position, float height, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Cylinder() { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Cylinders++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Cylinders++;
            }
        }

        public void DrawCone(ref Vector3 position, float height, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Cone() { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Cones++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Cones++;
            }
        }

        public void DrawLine(ref Vector3 start, ref Vector3 end, ref Color color, bool depthTest = true)
        {
            var cmd = new Line() { Start = start, End = end, Color = color };
            var msg = new Renderable(ref cmd);
            if (depthTest)
            {
                renderablesWithDepth.Add(msg);
                totalPrimitives.Lines++;
            }
            else
            {
                renderablesNoDepth.Add(msg);
                totalPrimitivesNoDepth.Lines++;
            }
        }

        protected override void InitializeCore()
        {

            var device = Context.GraphicsDevice;

            inputElements = VertexPositionTexture.Layout.CreateInputElements();
            lineInputElements = LineVertex.Layout.CreateInputElements();

            // create our pipeline state object
            pipelineState = new MutablePipelineState(device);
            pipelineState.State.SetDefaults();

            // TODO: create our associated effect
            primitiveEffect = new EffectInstance(Context.Effects.LoadEffect("PrimitiveShader").WaitForResult());
            primitiveEffect.UpdateEffect(device);

            lineEffect = new EffectInstance(Context.Effects.LoadEffect("LinePrimitiveShader").WaitForResult());
            lineEffect.UpdateEffect(device);

            // create initial vertex and index buffers
            var vertexData = new VertexPositionTexture[
                circle.Vertices.Length +
                plane.Vertices.Length +
                sphere.Vertices.Length +
                cube.Vertices.Length +
                capsule.Vertices.Length +
                cylinder.Vertices.Length +
                cone.Vertices.Length
            ];

            /* set up vertex buffer data */

            int vertexBufferOffset = 0;

            Array.Copy(circle.Vertices, vertexData, circle.Vertices.Length);
            primitiveVertexOffsets.Circles = vertexBufferOffset;
            vertexBufferOffset += circle.Vertices.Length;

            Array.Copy(plane.Vertices, 0, vertexData, vertexBufferOffset, plane.Vertices.Length);
            primitiveVertexOffsets.Quads = vertexBufferOffset;
            vertexBufferOffset += plane.Vertices.Length;

            Array.Copy(sphere.Vertices, 0, vertexData, vertexBufferOffset, sphere.Vertices.Length);
            primitiveVertexOffsets.Spheres = vertexBufferOffset;
            vertexBufferOffset += sphere.Vertices.Length;

            Array.Copy(cube.Vertices, 0, vertexData, vertexBufferOffset, cube.Vertices.Length);
            primitiveVertexOffsets.Cubes = vertexBufferOffset;
            vertexBufferOffset += cube.Vertices.Length;

            Array.Copy(capsule.Vertices, 0, vertexData, vertexBufferOffset, capsule.Vertices.Length);
            primitiveVertexOffsets.Capsules = vertexBufferOffset;
            vertexBufferOffset += capsule.Vertices.Length;

            Array.Copy(cylinder.Vertices, 0, vertexData, vertexBufferOffset, cylinder.Vertices.Length);
            primitiveVertexOffsets.Cylinders = vertexBufferOffset;
            vertexBufferOffset += cylinder.Vertices.Length;

            Array.Copy(cone.Vertices, 0, vertexData, vertexBufferOffset, cone.Vertices.Length);
            primitiveVertexOffsets.Cones = vertexBufferOffset;
            vertexBufferOffset += cone.Vertices.Length;

            var newVertexBuffer = Buffer.Vertex.New<VertexPositionTexture>(device, vertexData);
            vertexBuffer = newVertexBuffer;

            /* set up index buffer data */

            var indexData = new int[
                circle.Indices.Length +
                plane.Indices.Length +
                sphere.Indices.Length +
                cube.Indices.Length +
                capsule.Indices.Length +
                cylinder.Indices.Length +
                cone.Indices.Length
            ];

            int indexBufferOffset = 0;

            Array.Copy(circle.Indices, indexData, circle.Indices.Length);
            primitiveIndexOffsets.Circles = indexBufferOffset;
            indexBufferOffset += circle.Indices.Length;

            Array.Copy(plane.Indices, 0, indexData, indexBufferOffset, plane.Indices.Length);
            primitiveIndexOffsets.Quads = indexBufferOffset;
            indexBufferOffset += plane.Indices.Length;

            Array.Copy(sphere.Indices, 0, indexData, indexBufferOffset, sphere.Indices.Length);
            primitiveIndexOffsets.Spheres = indexBufferOffset;
            indexBufferOffset += sphere.Indices.Length;

            Array.Copy(cube.Indices, 0, indexData, indexBufferOffset, cube.Indices.Length);
            primitiveIndexOffsets.Cubes = indexBufferOffset;
            indexBufferOffset += cube.Indices.Length;

            Array.Copy(capsule.Indices, 0, indexData, indexBufferOffset, capsule.Indices.Length);
            primitiveIndexOffsets.Capsules = indexBufferOffset;
            indexBufferOffset += capsule.Indices.Length;

            Array.Copy(cylinder.Indices, 0, indexData, indexBufferOffset, cylinder.Indices.Length);
            primitiveIndexOffsets.Cylinders = indexBufferOffset;
            indexBufferOffset += cylinder.Indices.Length;

            Array.Copy(cone.Indices, 0, indexData, indexBufferOffset, cone.Indices.Length);
            primitiveIndexOffsets.Cones = indexBufferOffset;
            indexBufferOffset += cone.Indices.Length;

            var newIndexBuffer = Buffer.Index.New<int>(device, indexData);
            indexBuffer = newIndexBuffer;

            // allocate our buffers with position/colour etc data
            var newTransformBuffer = Buffer.Structured.New<Matrix>(device, 1);
            transformBuffer = newTransformBuffer;

            var newColourBuffer = Buffer.Structured.New<Color>(device, colors.Items);
            colorBuffer = newColourBuffer;

            var newLineVertexBuffer = Buffer.Vertex.New<LineVertex>(device, lineVertices.Items, GraphicsResourceUsage.Dynamic);
            lineVertexBuffer = newLineVertexBuffer;

        }

        public override void Extract()
        {

            void ProcessRenderables(FastList<Renderable> renderables, ref Primitives offsets)
            {

                for (int i = 0; i < renderables.Count; ++i)
                {
                    ref var cmd = ref renderables.Items[i];
                    switch (cmd.Type)
                    {
                        case RenderableType.Quad:
                            instances.Items[offsets.Quads].Position = cmd.QuadData.Position;
                            instances.Items[offsets.Quads].Rotation = cmd.QuadData.Rotation;
                            instances.Items[offsets.Quads].Scale = new Vector3(cmd.QuadData.Size.X, 1.0f, cmd.QuadData.Size.Y);
                            instances.Items[offsets.Quads].Color = cmd.QuadData.Color;
                            offsets.Quads++;
                            break;
                        case RenderableType.Circle:
                            instances.Items[offsets.Circles].Position = cmd.CircleData.Position;
                            instances.Items[offsets.Circles].Rotation = cmd.CircleData.Rotation;
                            instances.Items[offsets.Circles].Scale = new Vector3(cmd.CircleData.Radius * 2.0f, 0.0f, cmd.CircleData.Radius * 2.0f);
                            instances.Items[offsets.Circles].Color = cmd.CircleData.Color;
                            offsets.Circles++;
                            break;
                        case RenderableType.Sphere:
                            instances.Items[offsets.Spheres].Position = cmd.SphereData.Position;
                            instances.Items[offsets.Spheres].Rotation = Quaternion.Identity;
                            instances.Items[offsets.Spheres].Scale = new Vector3(cmd.SphereData.Radius * 2);
                            instances.Items[offsets.Spheres].Color = cmd.SphereData.Color;
                            offsets.Spheres++;
                            break;
                        case RenderableType.Cube:
                            ref var start = ref cmd.CubeData.Start;
                            ref var end = ref cmd.CubeData.End;
                            instances.Items[offsets.Cubes].Position = start;
                            instances.Items[offsets.Cubes].Rotation = cmd.CubeData.Rotation;
                            instances.Items[offsets.Cubes].Scale = end - start;
                            instances.Items[offsets.Cubes].Color = cmd.CubeData.Color;
                            offsets.Cubes++;
                            break;
                        case RenderableType.Capsule:
                            instances.Items[offsets.Capsules].Position = cmd.CapsuleData.Position;
                            instances.Items[offsets.Capsules].Rotation = cmd.CapsuleData.Rotation;
                            instances.Items[offsets.Capsules].Scale = new Vector3(cmd.CapsuleData.Radius * 2.0f, cmd.CapsuleData.Height, cmd.CapsuleData.Radius * 2.0f);
                            instances.Items[offsets.Capsules].Color = cmd.CapsuleData.Color;
                            offsets.Capsules++;
                            break;
                        case RenderableType.Cylinder:
                            instances.Items[offsets.Cylinders].Position = cmd.CylinderData.Position;
                            instances.Items[offsets.Cylinders].Rotation = cmd.CylinderData.Rotation;
                            instances.Items[offsets.Cylinders].Scale = new Vector3(cmd.CylinderData.Radius * 2.0f, cmd.CylinderData.Height, cmd.CylinderData.Radius * 2.0f);
                            instances.Items[offsets.Cylinders].Color = cmd.CylinderData.Color;
                            offsets.Cylinders++;
                            break;
                        case RenderableType.Cone:
                            instances.Items[offsets.Cones].Position = cmd.ConeData.Position;
                            instances.Items[offsets.Cones].Rotation = cmd.ConeData.Rotation;
                            instances.Items[offsets.Cones].Scale = new Vector3(cmd.ConeData.Radius * 2.0f, cmd.ConeData.Height, cmd.ConeData.Radius * 2.0f);
                            instances.Items[offsets.Cones].Color = cmd.ConeData.Color;
                            offsets.Cones++;
                            break;
                        case RenderableType.Line:
                            lineVertices.Items[offsets.Lines].Position = cmd.LineData.Start;
                            lineVertices.Items[offsets.Lines++].Color = cmd.LineData.Color;
                            lineVertices.Items[offsets.Lines].Position = cmd.LineData.End;
                            lineVertices.Items[offsets.Lines++].Color = cmd.LineData.Color;
                            break;
                    }
                }

            }

            int SumBasicPrimitives(ref Primitives primitives)
            {
                return primitives.Quads
                    + primitives.Circles
                    + primitives.Spheres
                    + primitives.Cubes
                    + primitives.Capsules
                    + primitives.Cylinders
                    + primitives.Cones;
            }

            Primitives SetupPrimitiveOffsets(ref Primitives counts, int offset = 0)
            {
                var offsets = new Primitives();
                offsets.Quads = 0 + offset;
                offsets.Circles = offsets.Quads + counts.Quads;
                offsets.Spheres = offsets.Circles + counts.Circles;
                offsets.Cubes = offsets.Spheres + counts.Spheres;
                offsets.Capsules = offsets.Cubes + counts.Cubes;
                offsets.Cylinders = offsets.Capsules + counts.Capsules;
                offsets.Cones = offsets.Cylinders + counts.Cylinders;
                return offsets;
            }

            /* everything except lines is included here, as lines just get accumulated into a buffer directly */
            int primitivesWithDepth = SumBasicPrimitives(ref totalPrimitives);
            int primitivesWithoutDepth = SumBasicPrimitives(ref totalPrimitivesNoDepth);
            int totalThingsToDraw = primitivesWithDepth + primitivesWithoutDepth;

            instances.Resize(totalThingsToDraw, true);

            lineVertices.Resize((totalPrimitives.Lines * 2) + (totalPrimitivesNoDepth.Lines * 2), true);

            var primitiveOffsets = SetupPrimitiveOffsets(ref totalPrimitives);
            var primitiveOffsetsNoDepth = SetupPrimitiveOffsets(ref totalPrimitivesNoDepth, primitivesWithDepth);

            /* line rendering data, separate buffer so offset isnt relative to the other data */
            primitiveOffsets.Lines = 0;
            primitiveOffsetsNoDepth.Lines = totalPrimitives.Lines * 2;

            /* save instance offsets before we mutate them as we need them when rendering */
            instanceOffsets = primitiveOffsets;
            instanceOffsetsNoDepth = primitiveOffsetsNoDepth;

            ProcessRenderables(renderablesWithDepth, ref primitiveOffsets);
            ProcessRenderables(renderablesNoDepth, ref primitiveOffsetsNoDepth);

            primitivesToDraw = totalPrimitives;
            primitivesToDrawNoDepth = totalPrimitivesNoDepth;

            renderablesWithDepth.Clear(true);
            renderablesNoDepth.Clear(true);
            totalPrimitives.Clear();
            totalPrimitivesNoDepth.Clear();

        }

        private unsafe static void UpdateBufferIfNecessary(GraphicsDevice device, CommandList commandList, ref Buffer buffer, DataPointer dataPtr, int elementSize)
        {
            int neededBufferSize = dataPtr.Size / elementSize;
            if (neededBufferSize > buffer.ElementCount)
            {
                buffer.Dispose();
                var newBuffer = Xenko.Graphics.Buffer.New(
                    device,
                    dataPtr,
                    buffer.StructureByteStride,
                    buffer.Flags
                );
                buffer = newBuffer;
            }
            else
            {
                buffer.SetData(commandList, dataPtr);
            }
        }

        private void CheckBuffers(RenderDrawContext context)
        {

            unsafe
            {

                fixed (Matrix* transformsPtr = transforms.Items)
                {
                    UpdateBufferIfNecessary(
                        context.GraphicsDevice, context.CommandList, buffer: ref transformBuffer,
                        dataPtr: new DataPointer(transformsPtr, transforms.Count * Marshal.SizeOf<Matrix>()),
                        elementSize: Marshal.SizeOf<Matrix>()
                    );
                }

                fixed (Color* colorsPtr = colors.Items)
                {
                    UpdateBufferIfNecessary(
                        context.GraphicsDevice, context.CommandList, buffer: ref colorBuffer,
                        dataPtr: new DataPointer(colorsPtr, colors.Count * Marshal.SizeOf<Color>()),
                        elementSize: Marshal.SizeOf<Color>()
                    );
                }

                fixed (LineVertex* lineVertsPtr = lineVertices.Items)
                {
                    UpdateBufferIfNecessary(
                        context.GraphicsDevice, context.CommandList, buffer: ref lineVertexBuffer,
                        dataPtr: new DataPointer(lineVertsPtr, lineVertices.Count * Marshal.SizeOf<LineVertex>()),
                        elementSize: Marshal.SizeOf<LineVertex>()
                    );
                }

            }

        }

        public override void Prepare(RenderDrawContext context)
        {

            transforms.Resize(instances.Count, true);
            colors.Resize(instances.Count, true);

            Dispatcher.For(0, transforms.Count, (int i) =>
            {
                ref var instance = ref instances.Items[i];
                Matrix.Transformation(ref instance.Scale, ref instance.Rotation, ref instance.Position, out transforms.Items[i]);
                colors[i] = instance.Color;
            }
            );

            CheckBuffers(context);

        }

        private void SetPrimitiveRenderingPipelineState(CommandList commandList, bool depthTest, FillMode selectedFillMode)
        {
            pipelineState.State.SetDefaults();
            pipelineState.State.PrimitiveType = PrimitiveType.TriangleList;
            pipelineState.State.RootSignature = primitiveEffect.RootSignature;
            pipelineState.State.EffectBytecode = primitiveEffect.Effect.Bytecode;
            pipelineState.State.DepthStencilState =
                (depthTest) ?
                    ((selectedFillMode == FillMode.Solid) ? DepthStencilStates.Default : DepthStencilStates.DepthRead)
                    : DepthStencilStates.None;
            pipelineState.State.RasterizerState.FillMode = selectedFillMode;
            pipelineState.State.RasterizerState.CullMode = CullMode.None;
            pipelineState.State.BlendState = BlendStates.NonPremultiplied;
            pipelineState.State.Output.CaptureState(commandList);
            pipelineState.State.InputElements = inputElements;
            pipelineState.Update();
        }

        private void SetLineRenderingPipelineState(CommandList commandList, bool depthTest)
        {
            pipelineState.State.SetDefaults();
            pipelineState.State.PrimitiveType = PrimitiveType.LineList;
            pipelineState.State.RootSignature = lineEffect.RootSignature;
            pipelineState.State.EffectBytecode = lineEffect.Effect.Bytecode;
            pipelineState.State.DepthStencilState = (depthTest) ? DepthStencilStates.DepthRead : DepthStencilStates.None;
            pipelineState.State.RasterizerState.FillMode = FillMode.Solid;
            pipelineState.State.RasterizerState.CullMode = CullMode.None;
            pipelineState.State.BlendState = BlendStates.AlphaBlend;
            pipelineState.State.Output.CaptureState(commandList);
            pipelineState.State.InputElements = lineInputElements;
            pipelineState.Update();
        }

        private void RenderPrimitives(RenderDrawContext context, RenderView renderView, ref Primitives offsets, ref Primitives counts, bool depthTest)
        {

            var commandList = context.CommandList;

            // set buffers and our current pipeline state
            commandList.SetVertexBuffer(0, vertexBuffer, 0, VertexPositionTexture.Layout.VertexStride);
            commandList.SetIndexBuffer(indexBuffer, 0, is32bits: true);
            commandList.SetPipelineState(pipelineState.CurrentState);

            // we set line width to something absurdly high to avoid having to alter our shader substantially for now
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.LineWidthMultiplier, (currentFillMode == FillMode.Solid) ? 10000.0f : 1.0f);
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.ViewProjection, renderView.ViewProjection);
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.Transforms, transformBuffer);
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.Colors, colorBuffer);

            primitiveEffect.UpdateEffect(context.GraphicsDevice);
            primitiveEffect.Apply(context.GraphicsContext);

            // draw spheres
            if (counts.Spheres > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Spheres);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(sphere.Indices.Length, counts.Spheres, primitiveIndexOffsets.Spheres, primitiveVertexOffsets.Spheres);

            }

            // draw quads
            if (counts.Quads > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Quads);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(plane.Indices.Length, counts.Quads, primitiveIndexOffsets.Quads, primitiveVertexOffsets.Quads);

            }

            // draw circles
            if (counts.Circles > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Circles);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(circle.Indices.Length, counts.Circles, primitiveIndexOffsets.Circles, primitiveVertexOffsets.Circles);

            }

            // draw cubes
            if (counts.Cubes > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Cubes);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(cube.Indices.Length, counts.Cubes, primitiveIndexOffsets.Cubes, primitiveVertexOffsets.Cubes);

            }

            // draw capsules
            if (counts.Capsules > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Capsules);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(capsule.Indices.Length, counts.Capsules, primitiveIndexOffsets.Capsules, primitiveVertexOffsets.Capsules);

            }

            // draw cylinders
            if (counts.Cylinders > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Cylinders);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(cylinder.Indices.Length, counts.Cylinders, primitiveIndexOffsets.Cylinders, primitiveVertexOffsets.Cylinders);

            }

            // draw cones
            if (counts.Cones > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, offsets.Cones);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(cone.Indices.Length, counts.Cones, primitiveIndexOffsets.Cones, primitiveVertexOffsets.Cones);

            }

            // draw lines
            if (counts.Lines > 0)
            {

                SetLineRenderingPipelineState(commandList, depthTest);
                commandList.SetVertexBuffer(0, lineVertexBuffer, 0, LineVertex.Layout.VertexStride);
                commandList.SetPipelineState(pipelineState.CurrentState);

                lineEffect.Parameters.Set(LinePrimitiveShaderKeys.ViewProjection, renderView.ViewProjection);
                lineEffect.UpdateEffect(context.GraphicsDevice);
                lineEffect.Apply(context.GraphicsContext);

                commandList.Draw(counts.Lines * 2, offsets.Lines);

            }

        }

        public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage)
        {

            RenderStage FindTransparentRenderStage(RenderSystem renderSystem)
            {
                for (int i = 0; i < renderSystem.RenderStages.Count; ++i)
                {
                    var stage = renderSystem.RenderStages[i];
                    if (stage.Name == "Transparent")
                    {
                        return stage;
                    }
                }
                return null;
            }

            // we only want to render in the transparent stage, is there a nicer way to do this?
            var transparentRenderStage = FindTransparentRenderStage(context.RenderContext.RenderSystem);
            var transparentRenderStageIndex = transparentRenderStage?.Index;

            // bail out if it's any other stage, this is crude but alas
            if (renderViewStage.Index != transparentRenderStageIndex)
            {
                return;
            }

            var commandList = context.CommandList;

            // update pipeline state, render with depth test first
            SetPrimitiveRenderingPipelineState(commandList, depthTest: true, selectedFillMode: currentFillMode);
            RenderPrimitives(context, renderView, ref instanceOffsets, ref primitivesToDraw, depthTest: true);

            // update pipeline state, render without depth test second
            SetPrimitiveRenderingPipelineState(commandList, depthTest: false, selectedFillMode: currentFillMode);
            RenderPrimitives(context, renderView, offsets: ref instanceOffsetsNoDepth, counts: ref primitivesToDrawNoDepth, depthTest: false);

        }

        public override void Flush(RenderDrawContext context)
        {
        }

        /* FIXME: is there a nicer way to handle dispose, some xenko idiom? */

        public override void Unload()
        {
            base.Unload();
            transformBuffer.Dispose();
            colorBuffer.Dispose();
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            lineVertexBuffer.Dispose();
        }

    }

}
