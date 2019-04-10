// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Linq;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Core.Threading;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Graphics.GeometricPrimitives;
using Xenko.Rendering;

using Buffer = Xenko.Graphics.Buffer;

namespace DebugRendering
{

    public class DebugSystem : GameSystemBase
    {

        internal enum DebugRenderableType : byte
        {
            Quad,
            Circle,
            Line,
            Box,
            Sphere,
            Capsule,
            Cylinder,
            Cone
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct DebugRenderable
        {

            public DebugRenderable(ref DebugDrawQuad q) : this()
            {
                Type = DebugRenderableType.Quad;
                QuadData = q;
            }

            public DebugRenderable(ref DebugDrawCircle c) : this()
            {
                Type = DebugRenderableType.Circle;
                CircleData = c;
            }

            public DebugRenderable(ref DebugDrawLine l) : this()
            {
                Type = DebugRenderableType.Line;
                LineData = l;
            }

            public DebugRenderable(ref DebugDrawBox b) : this()
            {
                Type = DebugRenderableType.Box;
                BoxData = b;
            }

            public DebugRenderable(ref DebugDrawSphere s) : this()
            {
                Type = DebugRenderableType.Sphere;
                SphereData = s;
            }

            public DebugRenderable(ref DebugDrawCapsule c) : this()
            {
                Type = DebugRenderableType.Capsule;
                CapsuleData = c;
            }

            public DebugRenderable(ref DebugDrawCylinder c) : this()
            {
                Type = DebugRenderableType.Cylinder;
                CylinderData = c;
            }

            public DebugRenderable(ref DebugDrawCone c) : this()
            {
                Type = DebugRenderableType.Cone;
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
            public DebugDrawBox BoxData;

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
            public Rectangle Rectangle;
            public Color Color;
        }

        internal struct DebugDrawCircle
        {
            public Vector2 Position;
            public float Radius;
            public Color Color;
        }

        internal struct DebugDrawLine
        {
            public Vector3 Start;
            public Vector3 End;
            public Quaternion Rotation;
            public Color Color;
        }

        internal struct DebugDrawBox
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

        static private Comparer<DebugRenderable> renderableComparer =
            Comparer<DebugRenderable>.Create((a, b) => a.Lifetime > b.Lifetime ? 1 : a.Lifetime < b.Lifetime ? -1 : 0);

        private readonly FastList<DebugRenderable> renderMessages = new FastList<DebugRenderable>();
        private readonly FastList<DebugRenderable> renderMessagesWithLifetime = new FastList<DebugRenderable>();

        /* FIXME: this is set from outside atm, bit of a hack */
        public DebugRenderFeature PrimitiveRenderer;
        public EffectInstance PrimitiveEffect;

        public DebugSystem(IServiceRegistry registry) : base(registry)
        {
            Enabled = true;
            Visible = Platform.IsRunningDebugAssembly;

            DrawOrder = 0xffffff;
            UpdateOrder = -100100; //before script
        }

        protected override void Destroy()
        {
            base.Destroy();
        }

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushMessage(ref DebugRenderable msg)
        {
            if (msg.Lifetime > 0.0f)
            {
                renderMessagesWithLifetime.Add(msg);
                // drop one old message if the tail size has been reached
                if (renderMessagesWithLifetime.Count > TimedTailSize)
                {
                    renderMessagesWithLifetime.RemoveAt(renderMessagesWithLifetime.Count - 1);
                }
            }
            else
            {
                renderMessages.Add(msg);
                // drop one old message if the tail size has been reached
                if (renderMessages.Count > NormalTailSize)
                {
                    renderMessages.RemoveAt(renderMessages.Count - 1);
                }
            }
        }

        public void DrawLine(Vector3 start, Vector3 end, float duration = 0.0f, bool depthTest = true)
        {
            DrawLine(start, end, PrimitiveColor, duration, depthTest);
        }

        public void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawLine { Start = start, End = end, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawLines(Vector3[] vertices, float duration = 0.0f, bool depthTest = true)
        {
            DrawLines(vertices, PrimitiveColor, duration, depthTest);
        }

        public void DrawLines(Vector3[] vertices, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var totalVertexPairs = vertices.Length - (vertices.Length % 2);
            for (int i = 0; i < totalVertexPairs; i += 2)
            {
                ref var v1 = ref vertices[i];
                ref var v2 = ref vertices[i];
                DrawLine(v1, v2, color, duration);
            }
        }

        public void DrawRay(Vector3 start, Vector3 dir, float duration = 0.0f, bool depthTest = true)
        {
            DrawRay(start, dir, PrimitiveColor, duration, depthTest);
        }

        public void DrawRay(Vector3 start, Vector3 dir, Color color, float duration = 0.0f, bool depthTest = true)
        {
            DrawLine(start, start + dir, color, duration, depthTest);
        }

        public void DrawRays(Vector3[] vertices, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var totalVertexPairs = vertices.Length - (vertices.Length % 2);
            for (int i = 0; i < totalVertexPairs; i += 2)
            {
                ref var v1 = ref vertices[i];
                ref var v2 = ref vertices[i];
                DrawLine(v1, v1 + v2, color, duration);
            }
        }

        public void DrawArrow(Vector3 from, Vector3 to, float duration = 0.0f, bool depthTest = true)
        {
            DrawArrow(from, to, PrimitiveColor, duration, depthTest);
        }

        public void DrawArrow(Vector3 from, Vector3 to, Color color, float duration = 0.0f, bool depthTest = true)
        {

        }

        public void DrawSphere(Vector3 position, float radius, float duration = 0.0f, bool depthTest = true)
        {
            DrawSphere(position, radius, PrimitiveColor, duration, depthTest);
        }

        public void DrawSphere(Vector3 position, float radius, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawSphere { Position = position, Radius = radius, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawSpheres(Vector3[] positions, float radius, float duration = 0.0f, bool depthTest = true)
        {
            DrawSpheres(positions, radius, PrimitiveColor, duration, depthTest);
        }

        public void DrawSpheres(Vector3[] positions, float radius, Color color, float duration = 0.0f, bool depthTest = true)
        {
            for (int i = 0; i < positions.Length; ++i)
            {
                ref var pos = ref positions[i];
                DrawSphere(pos, radius, color, duration, depthTest);
            }
        }

        public void DrawBounds(Vector3 start, Vector3 end, Quaternion rotation, float duration = 0.0f, bool depthTest = true)
        {
            DrawBounds(start, end, rotation, PrimitiveColor, duration, depthTest);
        }

        public void DrawBounds(Vector3 start, Vector3 end, Quaternion rotation, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawBox { Position = start, End = end, Rotation = rotation, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCube(Vector3 start, Vector3 size, Quaternion rotation, float duration = 0.0f, bool depthTest = true)
        {
            DrawCube(start, size, rotation, PrimitiveColor, duration, depthTest);
        }

        public void DrawCube(Vector3 start, Vector3 size, Quaternion rotation, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawBox { Position = start, End = start + size, Rotation = rotation, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCubes(Vector3[] positions, Quaternion[] rotations, Vector3 size, float duration = 0.0f, bool depthTest = true)
        {
            DrawCubes(positions, rotations, size, PrimitiveColor, duration, depthTest);
        }

        public void DrawCubes(Vector3[] positions, Quaternion[] rotations, Vector3 size, Color color, float duration = 0.0f, bool depthTest = true)
        {
            for (int i = 0; i < positions.Length; ++i)
            {
                ref var pos = ref positions[i];
                ref var rot = ref rotations[i];
                DrawCube(pos, size, rot, color, duration, depthTest);
            }
        }

        public void DrawCapsule(Vector3 position, float height, float radius, Quaternion rotation, float duration = 0.0f, bool depthTest = true)
        {
            DrawCapsule(position, height, radius, rotation, PrimitiveColor, duration, depthTest);
        }

        public void DrawCapsule(Vector3 position, float height, float radius, Quaternion rotation, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCapsule { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCylinder(Vector3 position, float height, float radius, Quaternion rotation, float duration = 0.0f, bool depthTest = true)
        {
            DrawCylinder(position, height, radius, rotation, PrimitiveColor, duration, depthTest);
        }

        public void DrawCylinder(Vector3 position, float height, float radius, Quaternion rotation, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCylinder { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public void DrawCone(Vector3 position, float height, float radius, Quaternion rotation, float duration = 0.0f, bool depthTest = true)
        {
            DrawCone(position, height, radius, rotation, PrimitiveColor, duration, depthTest);
        }

        public void DrawCone(Vector3 position, float height, float radius, Quaternion rotation, Color color, float duration = 0.0f, bool depthTest = true)
        {
            var cmd = new DebugDrawCone { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            var msg = new DebugRenderable(ref cmd) { Lifetime = duration };
            PushMessage(ref msg);
        }

        public ref Color PrimitiveColor { get { return ref primitiveColor; } }
        private Color primitiveColor = Color.LightGreen;

        public int NormalTailSize { get; set; } = 100;
        public int TimedTailSize { get; set; } = 100;

        public override void Update(GameTime gameTime)
        {

            HandlePrimitives(gameTime, renderMessages);
            HandlePrimitives(gameTime, renderMessagesWithLifetime);

            float delta = (float)gameTime.Elapsed.TotalSeconds;

            /* clear out any messages with no lifetime left */
            renderMessagesWithLifetime.ForEach((msg) => msg.Lifetime -= delta);
            renderMessagesWithLifetime.RemoveAll((msg) => msg.Lifetime <= 0.0f);

            /* just clear our per-frame array */
            renderMessages.Clear(true);

        }

        private void HandlePrimitives(GameTime gameTime, FastList<DebugRenderable> messages)
        {

            if (messages.Count == 0) return;

            for (int i = 0; i < messages.Count; ++i)
            {
                ref var msg = ref messages.Items[i];
                switch (msg.Type)
                {
                    case DebugRenderableType.Quad:
                        // PrimitiveRenderer.DrawQuad
                        break;
                    case DebugRenderableType.Circle:
                        // PrimitiveRenderer.DrawCircle
                        break;
                    case DebugRenderableType.Line:
                        PrimitiveRenderer.DrawLine(ref msg.LineData.Start, ref msg.LineData.End, ref msg.LineData.Color);
                        break;
                    case DebugRenderableType.Box:
                        PrimitiveRenderer.DrawBox(ref msg.BoxData.Position, ref msg.BoxData.End, ref msg.BoxData.Rotation, ref msg.BoxData.Color);
                        break;
                    case DebugRenderableType.Sphere:
                        PrimitiveRenderer.DrawSphere(ref msg.SphereData.Position, msg.SphereData.Radius, ref msg.SphereData.Color);
                        break;
                    case DebugRenderableType.Capsule:
                        PrimitiveRenderer.DrawCapsule(ref msg.CapsuleData.Position, msg.CapsuleData.Height, msg.CapsuleData.Radius, ref msg.CapsuleData.Rotation, ref msg.CapsuleData.Color);
                        break;
                    case DebugRenderableType.Cylinder:
                        PrimitiveRenderer.DrawCylinder(ref msg.CylinderData.Position, msg.CylinderData.Height, msg.CylinderData.Radius, ref msg.CylinderData.Rotation, ref msg.CylinderData.Color);
                        break;
                    case DebugRenderableType.Cone:
                        PrimitiveRenderer.DrawCone(ref msg.ConeData.Position, msg.ConeData.Height, msg.ConeData.Radius, ref msg.ConeData.Rotation, ref msg.ConeData.Color);
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
            public Vector3 TopLeft;
            public Vector3 BottomRight;
            public Color Color;
        }

        internal struct Circle
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Radius;
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

        const float DEFAULT_SPHERE_RADIUS = 0.5f;
        const float DEFAULT_CUBE_SIZE = 1.0f;
        const float DEFAULT_CAPSULE_LENGTH = 1.0f;
        const float DEFAULT_CAPSULE_RADIUS = 0.5f;
        const float DEFAULT_CYLINDER_HEIGHT = 1.0f;
        const float DEFAULT_CYLINDER_RADIUS = 0.5f;
        const float DEFAULT_PLANE_SIZE = 1.0f;
        const float DEFAULT_CONE_RADIUS = 0.5f;
        const float DEFAULT_CONE_HEIGHT = 1.0f;

        const int SPHERE_TESSELATION = 4;
        const int CAPSULE_TESSELATION = 2;
        const int CYLINDER_TESSELATION = 6;
        const int CONE_TESSELATION = 4;

        /* mesh data we will use when stuffing things in vertex buffers */
        private readonly GeometricMeshData<VertexPositionNormalTexture> plane = GeometricPrimitive.Plane.New(DEFAULT_PLANE_SIZE, DEFAULT_PLANE_SIZE, SPHERE_TESSELATION, SPHERE_TESSELATION);
        // private readonly GeometricMeshData<VertexPositionNormalTexture> circle = GeometricPrimitive.Plane.New(DEFAULT_PLANE_SIZE, DEFAULT_PLANE_SIZE);
        private readonly GeometricMeshData<VertexPositionNormalTexture> sphere = GeometricPrimitive.Sphere.New(DEFAULT_SPHERE_RADIUS, SPHERE_TESSELATION);
        private readonly GeometricMeshData<VertexPositionNormalTexture> cube = GeometricPrimitive.Cube.New(DEFAULT_CUBE_SIZE);
        private readonly GeometricMeshData<VertexPositionNormalTexture> capsule = GeometricPrimitive.Capsule.New(DEFAULT_CAPSULE_LENGTH, DEFAULT_CAPSULE_RADIUS, CAPSULE_TESSELATION);
        private readonly GeometricMeshData<VertexPositionNormalTexture> cylinder = GeometricPrimitive.Cylinder.New(DEFAULT_CYLINDER_HEIGHT, DEFAULT_CYLINDER_RADIUS, CYLINDER_TESSELATION);
        private readonly GeometricMeshData<VertexPositionNormalTexture> cone = GeometricPrimitive.Cone.New(DEFAULT_CONE_RADIUS, DEFAULT_CONE_HEIGHT, CONE_TESSELATION);

        /* gpu side vertex and index buffer for our primitive data */
        private Buffer vertexBuffer;
        private Buffer indexBuffer;

        private int quadVertexOffset = 0;
        private int sphereVertexOffset = 0;
        private int cubeVertexOffset = 0;
        private int capsuleVertexOffset = 0;
        private int cylinderVertexOffset = 0;
        private int coneVertexOffset = 0;

        private int quadIndexOffset = 0;
        private int sphereIndexOffset = 0;
        private int cubeIndexOffset = 0;
        private int capsuleIndexOffset = 0;
        private int cylinderIndexOffset = 0;
        private int coneIndexOffset = 0;

        /* other gpu related data */
        private MutablePipelineState pipelineState;
        private InputElementDescription[] inputElements;
        private EffectInstance primitiveEffect;
        private Buffer transformBuffer;
        private Buffer colorBuffer;

        /* messages */
        private readonly FastList<Renderable> renderables = new FastList<Renderable>();

        /* accumulators used when data is being pushed to the system */
        private int totalQuads = 0;
        private int totalCircles = 0;
        private int totalSpheres = 0;
        private int totalCubes = 0;
        private int totalCapsules = 0;
        private int totalCylinders = 0;
        private int totalCones = 0;
        private int totalLines = 0;
        
        /* used to specify offset into instance data buffers when drawing */
        private int quadInstanceOffset = 0;
        private int circleInstanceOffset = 0;
        private int sphereInstanceOffset = 0;
        private int cubeInstanceOffset = 0;
        private int capsuleInstanceOffset = 0;
        private int cylinderInstanceOffset = 0;
        private int coneInstanceOffset = 0;
        private int lineInstanceOffset = 0;

        /* used in render stage to know how many of each instance to draw */
        private int quadsToDraw = 0;
        private int circlesToDraw = 0;
        private int spheresToDraw = 0;
        private int cubesToDraw = 0;
        private int capsulesToDraw = 0;
        private int cylindersToDraw = 0;
        private int conesToDraw = 0;
        private int linesToDraw = 0;

        /* message related data */
        private readonly FastList<Matrix> transforms = new FastList<Matrix>(1);
        private readonly FastList<Vector3> positions = new FastList<Vector3>(1);
        private readonly FastList<Quaternion> rotations = new FastList<Quaternion>(1);
        private readonly FastList<Vector3> scales = new FastList<Vector3>(1);
        private readonly FastList<Color4> colors = new FastList<Color4>(1);

        public DebugRenderFeature()
        {
        }

        public void DrawQuad(ref Vector3 topLeft, ref Vector3 bottomRight, ref Color color, bool depthTest = true)
        {
            var cmd = new Quad() { TopLeft = topLeft, BottomRight = bottomRight, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalQuads++;
        }

        /*
        public void DrawCircle(ref Vector3 position, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Circle() { Position = position, Rotation = rotation, Color = color };
            renderables.Add(new Renderable(cmd));
            totalCircles++;
        }
        */

        public void DrawSphere(ref Vector3 position, float radius, ref Color color, bool depthTest = true)
        {
            var cmd = new Sphere() { Position = position, Radius = radius, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalSpheres++;
        }

        public void DrawBox(ref Vector3 start, ref Vector3 end, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Cube() { Start = start, End = end, Rotation = rotation, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalCubes++;
        }

        public void DrawCapsule(ref Vector3 position, float height, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Capsule() { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalCapsules++;
        }

        public void DrawCylinder(ref Vector3 position, float height, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Cylinder() { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalCylinders++;
        }

        public void DrawCone(ref Vector3 position, float height, float radius, ref Quaternion rotation, ref Color color, bool depthTest = true)
        {
            var cmd = new Cone() { Position = position, Height = height, Radius = radius, Rotation = rotation, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalCones++;
        }

        public void DrawLine(ref Vector3 start, ref Vector3 end, ref Color color, bool depthTest = true)
        {
            var cmd = new Line() { Start = start, End = end, Color = color };
            renderables.Add(new Renderable(ref cmd));
            totalLines++;
        }

        protected override void InitializeCore()
        {

            var device = Context.GraphicsDevice;

            inputElements = VertexPositionNormalTexture.Layout.CreateInputElements();

            // create our pipeline state object
            pipelineState = new MutablePipelineState(device);
            pipelineState.State.SetDefaults();
            pipelineState.State.InputElements = inputElements;
            pipelineState.State.PrimitiveType = PrimitiveType.TriangleList;
            pipelineState.State.RasterizerState.FillMode = FillMode.Wireframe;

            // TODO: create our associated effect
            primitiveEffect = new EffectInstance(Context.Effects.LoadEffect("PrimitiveShader").WaitForResult());
            primitiveEffect.UpdateEffect(device);

            // create initial vertex and index buffers
            var vertexData = new VertexPositionNormalTexture[
                plane.Vertices.Length +
                sphere.Vertices.Length +
                cube.Vertices.Length +
                capsule.Vertices.Length +
                cylinder.Vertices.Length +
                cone.Vertices.Length
            ];

            /* set up vertex buffer data */

            int vertexBufferOffset = 0;

            Array.Copy(plane.Vertices, vertexData, plane.Vertices.Length);
            quadVertexOffset = vertexBufferOffset;
            vertexBufferOffset += plane.Vertices.Length;

            Array.Copy(sphere.Vertices, 0, vertexData, vertexBufferOffset, sphere.Vertices.Length);
            sphereVertexOffset = vertexBufferOffset;
            vertexBufferOffset += sphere.Vertices.Length;

            Array.Copy(cube.Vertices, 0, vertexData, vertexBufferOffset, cube.Vertices.Length);
            cubeVertexOffset = vertexBufferOffset;
            vertexBufferOffset += cube.Vertices.Length;

            Array.Copy(capsule.Vertices, 0, vertexData, vertexBufferOffset, capsule.Vertices.Length);
            capsuleVertexOffset = vertexBufferOffset;
            vertexBufferOffset += capsule.Vertices.Length;

            Array.Copy(cylinder.Vertices, 0, vertexData, vertexBufferOffset, cylinder.Vertices.Length);
            cylinderVertexOffset = vertexBufferOffset;
            vertexBufferOffset += cylinder.Vertices.Length;

            Array.Copy(cone.Vertices, 0, vertexData, vertexBufferOffset, cone.Vertices.Length);
            coneVertexOffset = vertexBufferOffset;
            vertexBufferOffset += cone.Vertices.Length;

            var newVertexBuffer = Buffer.Vertex.New<VertexPositionNormalTexture>(device, vertexData);
            vertexBuffer = newVertexBuffer;

            /* set up index buffer data */

            var indexData = new int[
                plane.Indices.Length +
                sphere.Indices.Length +
                cube.Indices.Length +
                capsule.Indices.Length +
                cylinder.Indices.Length +
                cone.Indices.Length
            ];

            int indexBufferOffset = 0;

            Array.Copy(plane.Indices, indexData, plane.Indices.Length);
            quadIndexOffset = indexBufferOffset;
            indexBufferOffset += plane.Indices.Length;

            Array.Copy(sphere.Indices, 0, indexData, indexBufferOffset, sphere.Indices.Length);
            sphereIndexOffset = indexBufferOffset;
            indexBufferOffset += sphere.Indices.Length;

            Array.Copy(cube.Indices, 0, indexData, indexBufferOffset, cube.Indices.Length);
            cubeIndexOffset = indexBufferOffset;
            indexBufferOffset += cube.Indices.Length;

            Array.Copy(capsule.Indices, 0, indexData, indexBufferOffset, capsule.Indices.Length);
            capsuleIndexOffset = indexBufferOffset;
            indexBufferOffset += capsule.Indices.Length;

            Array.Copy(cylinder.Indices, 0, indexData, indexBufferOffset, cylinder.Indices.Length);
            cylinderIndexOffset = indexBufferOffset;
            indexBufferOffset += cylinder.Indices.Length;

            Array.Copy(cone.Indices, 0, indexData, indexBufferOffset, cone.Indices.Length);
            coneIndexOffset = indexBufferOffset;
            indexBufferOffset += cone.Indices.Length;

            var newIndexBuffer = Buffer.Index.New<int>(device, indexData);
            indexBuffer = newIndexBuffer;

            // allocate our buffers with position/colour etc data
            var newTransformBuffer = Buffer.Structured.New<Matrix>(device, 1);
            transformBuffer = newTransformBuffer;

            var newColourBuffer = Buffer.Structured.New<Color4>(device, colors.Items);
            colorBuffer = newColourBuffer;

        }

        public override void Extract()
        {

            int totalThingsToDraw =
                totalQuads
                + totalCircles
                + totalSpheres
                + totalCubes
                + totalCapsules
                + totalCylinders
                + totalCones
                + totalLines;

            positions.Resize(totalThingsToDraw, true);
            rotations.Resize(totalThingsToDraw - totalSpheres, true); // spheres have no rotation
            scales.Resize(totalThingsToDraw, true);
            colors.Resize(totalThingsToDraw, true);

            int sphereIndex = 0;
            int quadIndex = sphereIndex + totalSpheres;
            int circleIndex = quadIndex + totalQuads;
            int cubeIndex = circleIndex + totalCircles;
            int capsuleIndex = cubeIndex + totalCubes;
            int cylinderIndex = capsuleIndex + totalCapsules;
            int coneIndex = cylinderIndex + totalCylinders;
            int lineIndex = coneIndex + totalCones;

            /* save instance offsets before we mutate them as we need them when rendering */
            quadInstanceOffset = quadIndex;
            circleInstanceOffset = circleIndex;
            sphereInstanceOffset = sphereIndex;
            cubeInstanceOffset = cubeIndex;
            capsuleInstanceOffset = capsuleIndex;
            cylinderInstanceOffset = cylinderIndex;
            coneInstanceOffset = coneIndex;
            lineInstanceOffset = lineIndex;

            for (int i = 0; i < renderables.Count; ++i)
            {
                ref var cmd = ref renderables.Items[i];
                switch (cmd.Type)
                {
                    case RenderableType.Quad:
                        quadIndex++;
                        break;
                    case RenderableType.Circle:
                        circleIndex++;
                        break;
                    case RenderableType.Sphere:
                        positions[sphereIndex] = cmd.SphereData.Position;
                        scales[sphereIndex] = new Vector3(cmd.SphereData.Radius * 2);
                        colors[sphereIndex] = cmd.SphereData.Color;
                        sphereIndex++;
                        break;
                    case RenderableType.Cube:
                        positions[cubeIndex] = cmd.CubeData.Start;
                        rotations[cubeIndex - totalSpheres] = cmd.CubeData.Rotation;
                        scales[cubeIndex] = new Vector3(2.0f);
                        colors[cubeIndex] = cmd.CubeData.Color;
                        cubeIndex++;
                        break;
                    case RenderableType.Capsule:
                        positions[capsuleIndex] = cmd.CapsuleData.Position;
                        rotations[capsuleIndex - totalSpheres] = cmd.CapsuleData.Rotation;
                        scales[capsuleIndex] = new Vector3(cmd.CapsuleData.Radius * 2, cmd.CapsuleData.Height, cmd.CapsuleData.Radius * 2);
                        colors[capsuleIndex] = cmd.CapsuleData.Color;
                        capsuleIndex++;
                        break;
                    case RenderableType.Cylinder:
                        positions[cylinderIndex] = cmd.CylinderData.Position;
                        rotations[cylinderIndex - totalSpheres] = cmd.CylinderData.Rotation;
                        scales[cylinderIndex] = new Vector3(cmd.CylinderData.Radius * 2, cmd.CylinderData.Height, cmd.CylinderData.Radius * 2);
                        colors[cylinderIndex] = cmd.CylinderData.Color;
                        cylinderIndex++;
                        break;
                    case RenderableType.Cone:
                        positions[coneIndex] = cmd.ConeData.Position;
                        rotations[coneIndex - totalSpheres] = cmd.ConeData.Rotation;
                        scales[coneIndex] = new Vector3(cmd.ConeData.Radius * 2, cmd.ConeData.Height, cmd.ConeData.Radius * 2);
                        colors[coneIndex] = cmd.ConeData.Color;
                        coneIndex++;
                        break;
                    case RenderableType.Line:
                        lineIndex++;
                        break;
                }
            }

            quadsToDraw = totalQuads;
            circlesToDraw = totalCircles;
            spheresToDraw = totalSpheres;
            cubesToDraw = totalCubes;
            capsulesToDraw = totalCapsules;
            cylindersToDraw = totalCylinders;
            conesToDraw = totalCones;
            linesToDraw = totalLines;

            renderables.Clear(true);
            totalQuads = 0;
            totalCircles = 0;
            totalSpheres = 0;
            totalCubes = 0;
            totalCapsules = 0;
            totalCylinders = 0;
            totalCones = 0;
            totalLines = 0;

        }
        unsafe static void UpdateBufferIfNecessary(GraphicsDevice device, CommandList commandList, ref Buffer buffer, DataPointer dataPtr, int elementSize)
        {
            int neededBufferSize = dataPtr.Size / elementSize;
            if (neededBufferSize > buffer.ElementCount)
            {
                buffer.Dispose();
                var newBuffer = Xenko.Graphics.Buffer.Structured.New(
                    device,
                    dataPtr,
                    buffer.StructureByteStride,
                    isUnorderedAccess: true
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

                fixed (Color4* colorsPtr = colors.Items)
                {
                    UpdateBufferIfNecessary(
                        context.GraphicsDevice, context.CommandList, buffer: ref colorBuffer,
                        dataPtr: new DataPointer(colorsPtr, colors.Count * Marshal.SizeOf<Color4>()),
                        elementSize: Marshal.SizeOf<Color4>()
                    );
                }

            }

        }

        public override void Prepare(RenderDrawContext context)
        {

            transforms.Resize(positions.Count, true);

            /* transform only things without rotation first */
            Dispatcher.For(0, spheresToDraw, (int i) =>
            {
                var identQuat = Quaternion.Identity;
                Matrix.Transformation(ref scales.Items[i], ref identQuat, ref positions.Items[i], out transforms.Items[i]);
            }
            );

            /* start next dispatch at lower bound for things that have rotation, at this point only spheres dont */
            Dispatcher.For(spheresToDraw, transforms.Count, (int i) =>
            {
                Matrix.Transformation(ref scales.Items[i], ref rotations.Items[i - spheresToDraw], ref positions.Items[i], out transforms.Items[i]);
            }
            );

            CheckBuffers(context);

        }

        private RenderStage FindOpaqueRenderStage(RenderSystem renderSystem)
        {
            for (int i = 0; i < renderSystem.RenderStages.Count; ++i)
            {
                var stage = renderSystem.RenderStages[i];
                if (stage.Name == "Opaque")
                {
                    return stage;
                }
            }
            return null;
        }

        public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage)
        {

            // we only want to render in the opaque stage, is there a nicer way to do this?
            var opaqueRenderStage = FindOpaqueRenderStage(context.RenderContext.RenderSystem);
            var opaqueRenderStageIndex = opaqueRenderStage.Index;

            // bail out if it's any other stage, this is crude but alas
            if (renderViewStage.Index != opaqueRenderStageIndex) return;

            var device = context.GraphicsDevice;
            var commandList = context.CommandList;

            // update pipeline state
            pipelineState.State.SetDefaults();
            pipelineState.State.RootSignature = primitiveEffect.RootSignature;
            pipelineState.State.EffectBytecode = primitiveEffect.Effect.Bytecode;
            pipelineState.State.DepthStencilState = DepthStencilStates.Default;
            pipelineState.State.RasterizerState.FillMode = FillMode.Wireframe;
            pipelineState.State.RasterizerState.CullMode = CullMode.None;
            pipelineState.State.BlendState = BlendStates.AlphaBlend;
            pipelineState.State.Output.CaptureState(commandList);
            pipelineState.State.InputElements = inputElements;
            pipelineState.Update();

            // set buffers and our current pipeline state
            commandList.SetVertexBuffer(0, vertexBuffer, 0, VertexPositionNormalTexture.Layout.VertexStride);
            commandList.SetIndexBuffer(indexBuffer, 0, is32bits: true);
            commandList.SetPipelineState(pipelineState.CurrentState);

            // now set our parameters too
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.ViewProjection, renderView.ViewProjection);
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.Transforms, transformBuffer);
            primitiveEffect.Parameters.Set(PrimitiveShaderKeys.Colors, colorBuffer);

            primitiveEffect.UpdateEffect(device);
            primitiveEffect.Apply(context.GraphicsContext);

            /* finally render */

            // draw spheres
            if (spheresToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, sphereInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(sphere.Indices.Length, spheresToDraw, sphereIndexOffset, sphereVertexOffset);

            }

            // draw quads
            if (quadsToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, quadInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(plane.Indices.Length, quadsToDraw, quadIndexOffset, quadVertexOffset);

            }

            // draw cubes
            if (cubesToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, cubeInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(cube.Indices.Length, cubesToDraw, cubeIndexOffset, cubeVertexOffset);

            }

            // draw capsules
            if (capsulesToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, capsuleInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(capsule.Indices.Length, capsulesToDraw, capsuleIndexOffset, capsuleVertexOffset);

            }

            // draw cylinders
            if (cylindersToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, cylinderInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(cylinder.Indices.Length, cylindersToDraw, cylinderIndexOffset, cylinderVertexOffset);

            }

            // draw cones
            if (conesToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, coneInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                commandList.DrawIndexedInstanced(cone.Indices.Length, conesToDraw, coneIndexOffset, coneVertexOffset);

            }

            // draw lines
            if (linesToDraw > 0)
            {

                primitiveEffect.Parameters.Set(PrimitiveShaderKeys.InstanceOffset, lineInstanceOffset);
                primitiveEffect.Apply(context.GraphicsContext);

                // commandList.DrawIndexedInstanced(line.Indices.Length, linesToDraw, curIndexOffset, curVertexOffset);
                // curIndexOffset += line.Indices.Length;

            }

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
        }

    }

}
