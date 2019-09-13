// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Runtime.InteropServices;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Engine;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Rendering;

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

        private DebugRenderFeature.DebugRenderObject primitiveRenderer;

        public RenderingMode RenderMode { get; set; } = RenderingMode.Wireframe;

        public Color PrimitiveColor { get; set; } = Color.LightGreen;

        public int MaxPrimitives { get; set; } = 100;
        public int MaxPrimitivesWithLifetime { get; set; } = 100;

        public RenderGroupMask RenderGroup { get; set; } = RenderGroupMask.All;

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

        private bool CreateDebugRenderObject()
        {

            var sceneSystem = Services.GetService<SceneSystem>();
            if (sceneSystem == null) return false;

            var sceneInstance = sceneSystem.SceneInstance;
            VisibilityGroup visibilityGroup = null;

            foreach (var currentVisibilityGroup in sceneInstance.VisibilityGroups)
            {
                if (currentVisibilityGroup.RenderSystem == sceneSystem.GraphicsCompositor.RenderSystem)
                {
                    visibilityGroup = currentVisibilityGroup;
                    break;
                }
            }

            if (visibilityGroup == null) return false;
            var newDebugRenderObject = new DebugRenderFeature.DebugRenderObject();
            visibilityGroup.RenderObjects.Add(newDebugRenderObject);
            primitiveRenderer = newDebugRenderObject;

            return true;

        }

        public override void Update(GameTime gameTime)
        {

            if (!Enabled) return;

            if (primitiveRenderer == null)
            {
                bool created = CreateDebugRenderObject();
                if (!created) return;
            }

            switch (RenderMode) {
                case RenderingMode.Wireframe:
                    primitiveRenderer.CurrentFillMode = FillMode.Wireframe;
                    break;
                case RenderingMode.Solid:
                    primitiveRenderer.CurrentFillMode = FillMode.Solid;
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
                        primitiveRenderer.DrawQuad(ref msg.QuadData.Position, ref msg.QuadData.Size, ref msg.QuadData.Rotation, ref msg.QuadData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.QuadNoDepth:
                        primitiveRenderer.DrawQuad(ref msg.QuadData.Position, ref msg.QuadData.Size, ref msg.QuadData.Rotation, ref msg.QuadData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Circle:
                        primitiveRenderer.DrawCircle(ref msg.CircleData.Position, msg.CircleData.Radius, ref msg.CircleData.Rotation, ref msg.CircleData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CircleNoDepth:
                        primitiveRenderer.DrawCircle(ref msg.CircleData.Position, msg.CircleData.Radius, ref msg.CircleData.Rotation, ref msg.CircleData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Line:
                        primitiveRenderer.DrawLine(ref msg.LineData.Start, ref msg.LineData.End, ref msg.LineData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.LineNoDepth:
                        primitiveRenderer.DrawLine(ref msg.LineData.Start, ref msg.LineData.End, ref msg.LineData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Cube:
                        primitiveRenderer.DrawCube(ref msg.CubeData.Position, ref msg.CubeData.End, ref msg.CubeData.Rotation, ref msg.CubeData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CubeNoDepth:
                        primitiveRenderer.DrawCube(ref msg.CubeData.Position, ref msg.CubeData.End, ref msg.CubeData.Rotation, ref msg.CubeData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Sphere:
                        primitiveRenderer.DrawSphere(ref msg.SphereData.Position, msg.SphereData.Radius, ref msg.SphereData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.SphereNoDepth:
                        primitiveRenderer.DrawSphere(ref msg.SphereData.Position, msg.SphereData.Radius, ref msg.SphereData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Capsule:
                        primitiveRenderer.DrawCapsule(ref msg.CapsuleData.Position, msg.CapsuleData.Height, msg.CapsuleData.Radius, ref msg.CapsuleData.Rotation, ref msg.CapsuleData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CapsuleNoDepth:
                        primitiveRenderer.DrawCapsule(ref msg.CapsuleData.Position, msg.CapsuleData.Height, msg.CapsuleData.Radius, ref msg.CapsuleData.Rotation, ref msg.CapsuleData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Cylinder:
                        primitiveRenderer.DrawCylinder(ref msg.CylinderData.Position, msg.CylinderData.Height, msg.CylinderData.Radius, ref msg.CylinderData.Rotation, ref msg.CylinderData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.CylinderNoDepth:
                        primitiveRenderer.DrawCylinder(ref msg.CylinderData.Position, msg.CylinderData.Height, msg.CylinderData.Radius, ref msg.CylinderData.Rotation, ref msg.CylinderData.Color, depthTest: false);
                        break;
                    case DebugRenderableType.Cone:
                        primitiveRenderer.DrawCone(ref msg.ConeData.Position, msg.ConeData.Height, msg.ConeData.Radius, ref msg.ConeData.Rotation, ref msg.ConeData.Color, depthTest: true);
                        break;
                    case DebugRenderableType.ConeNoDepth:
                        primitiveRenderer.DrawCone(ref msg.ConeData.Position, msg.ConeData.Height, msg.ConeData.Radius, ref msg.ConeData.Rotation, ref msg.ConeData.Color, depthTest: false);
                        break;
                }
            }

        }

    }

}
