using System;
using System.Linq;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Core.Threading;
using Xenko.Engine;
using Xenko.Physics;
using Xenko.Rendering;

namespace DebugRendering
{

    public class DebugTest : SyncScript
    {

        enum CurRenderMode : byte
        {
            All = 0,
            Quad,
            Circle,
            Sphere,
            Cube,
            Capsule,
            Cylinder,
            Cone,
            Ray,
            Arrow,
            None
        }

        const int ChangePerSecond = 8192 + 2048;
        const int InitialNumPrimitives = 1024;
        const int AreaSize = 64;

        [DataMemberIgnore]
        DebugRenderSystem DebugDraw; // this is here to make it look like it should when properly integrated

        int minNumberofPrimitives = 0;
        int maxNumberOfPrimitives = 327680;
        int currentNumPrimitives = InitialNumPrimitives;
        CurRenderMode mode = CurRenderMode.All;
        bool useDepthTesting = true;
        bool useWireframe = true;
        bool running = true;

        FastList<Vector3> primitivePositions = new FastList<Vector3>(InitialNumPrimitives);
        FastList<Quaternion> primitiveRotations = new FastList<Quaternion>(InitialNumPrimitives);
        FastList<Vector3> primitiveVelocities = new FastList<Vector3>(InitialNumPrimitives);
        FastList<Vector3> primitiveRotVelocities = new FastList<Vector3>(InitialNumPrimitives);
        FastList<Color> primitiveColors = new FastList<Color>(InitialNumPrimitives);

        public CameraComponent CurrentCamera;

        private void InitializePrimitives(int from, int to)
        {

            var random = new Random();

            primitivePositions.Resize(to, true);
            primitiveRotations.Resize(to, true);
            primitiveVelocities.Resize(to, true);
            primitiveRotVelocities.Resize(to, true);
            primitiveColors.Resize(to, true);

            for (int i = from; i < to; ++i)
            {

                // initialize boxes

                var randX = random.Next(-AreaSize, AreaSize);
                var randY = random.Next(-AreaSize, AreaSize);
                var randZ = random.Next(-AreaSize, AreaSize);

                var velX = random.NextDouble() * 4.0;
                var velY = random.NextDouble() * 4.0;
                var velZ = random.NextDouble() * 4.0;
                var ballVel = new Vector3((float)velX, (float)velY, (float)velZ);

                var rotVelX = random.NextDouble();
                var rotVelY = random.NextDouble();
                var rotVelZ = random.NextDouble();
                var ballRotVel = new Vector3((float)rotVelX, (float)rotVelY, (float)rotVelZ);

                primitivePositions.Items[i] = new Vector3(randX, randY, randZ);
                primitiveRotations.Items[i] = Quaternion.Identity;
                primitiveVelocities.Items[i] = ballVel;
                primitiveRotVelocities.Items[i] = ballRotVel;

                ref var color = ref primitiveColors.Items[i];
                color.R = (byte)(((primitivePositions[i].X / AreaSize) + 1f) / 2.0f * 255.0f);
                color.G = (byte)(((primitivePositions[i].Y / AreaSize) + 1f) / 2.0f * 255.0f);
                color.B = (byte)(((primitivePositions[i].Z / AreaSize) + 1f) / 2.0f * 255.0f);
                color.A = 255;

            }
        }

        public override void Start()
        {

            RenderStage FindRenderStage(RenderSystem renderSystem, string name)
            {
                for (int i = 0; i < renderSystem.RenderStages.Count; ++i)
                {
                    var stage = renderSystem.RenderStages[i];
                    if (stage.Name == name)
                    {
                        return stage;
                    }
                }
                return null;
            }

            DebugDraw = new DebugRenderSystem(Services);
            DebugDraw.PrimitiveColor = Color.Green;
            DebugDraw.MaxPrimitives = (currentNumPrimitives * 2) + 8;
            DebugDraw.MaxPrimitivesWithLifetime = (currentNumPrimitives * 2) + 8;

            // FIXME
            var debugRenderFeatures = SceneSystem.GraphicsCompositor.RenderFeatures.OfType<DebugRenderFeature>();
            var opaqueRenderStage = FindRenderStage(SceneSystem.GraphicsCompositor.RenderSystem, "Opaque");

            if (!debugRenderFeatures.Any())
            {
                var newDebugRenderFeature = new DebugRenderFeature() {
                    RenderStageSelectors = {
                        new SimpleGroupToRenderStageSelector
                        {
                            RenderStage = opaqueRenderStage
                        },
                    }
                };
                SceneSystem.GraphicsCompositor.RenderFeatures.Add(newDebugRenderFeature);
            }

            // keep DebugText visible in release builds too
            DebugText.Visible = true;
            Services.AddService(DebugDraw);
            Game.GameSystems.Add(DebugDraw);

            InitializePrimitives(0, currentNumPrimitives);

        }

        private int Clamp(int v, int min, int max)
        {
            if (v < min)
            {
                return min;
            }
            else if (v > max)
            {
                return max;
            }
            else
            {
                return v;
            }
        }

        public override void Update()
        {

            var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;

            var newAmountOfBoxes = Clamp(currentNumPrimitives + (int)(Input.MouseWheelDelta * ChangePerSecond * dt), minNumberofPrimitives, maxNumberOfPrimitives);

            if (Input.IsKeyPressed(Xenko.Input.Keys.LeftAlt))
            {
                mode = (CurRenderMode)(((int)mode + 1) % ((int)CurRenderMode.None + 1));
            }

            if (Input.IsKeyPressed(Xenko.Input.Keys.LeftCtrl))
            {
                useDepthTesting = !useDepthTesting;
            }

            if (Input.IsKeyPressed(Xenko.Input.Keys.Tab))
            {
                useWireframe = !useWireframe;
            }

            if (Input.IsKeyPressed(Xenko.Input.Keys.Space))
            {
                running = !running;
            }

            if (newAmountOfBoxes > currentNumPrimitives)
            {
                InitializePrimitives(currentNumPrimitives, newAmountOfBoxes);
                DebugDraw.MaxPrimitivesWithLifetime = (newAmountOfBoxes * 2) + 8;
                DebugDraw.MaxPrimitives = (newAmountOfBoxes * 2) + 8;
                currentNumPrimitives = newAmountOfBoxes;
            }
            else
            {
                currentNumPrimitives = newAmountOfBoxes;
            }

            int textPositionX = (int)Input.Mouse.SurfaceSize.X - 384;
            DebugText.Print($"Primitive Count: {currentNumPrimitives} (scrollwheel to adjust)",
                new Int2(textPositionX, 32));

            DebugText.Print($" - Render Mode: {mode} (left alt to switch)",
                new Int2(textPositionX, 48));

            DebugText.Print($" - Depth Testing: {(useDepthTesting ? "On " : "Off")} (left ctrl to toggle)",
                new Int2(textPositionX, 64));

            DebugText.Print($" - Fillmode: {(useWireframe ? "Wireframe" : "Solid")} (tab to toggle)",
                new Int2(textPositionX, 80));

            DebugText.Print($" - State: {(running ? "Simulating" : "Paused")} (space to toggle)",
                new Int2(textPositionX, 96));

            if (running)
            {
                Dispatcher.For(0, currentNumPrimitives, i =>
                {

                    ref var position = ref primitivePositions.Items[i];
                    ref var velocity = ref primitiveVelocities.Items[i];
                    ref var rotVelocity = ref primitiveRotVelocities.Items[i];
                    ref var rotation = ref primitiveRotations.Items[i];
                    ref var color = ref primitiveColors.Items[i];

                    if (position.X > AreaSize || position.X < -AreaSize)
                    {
                        velocity.X = -velocity.X;
                    }

                    if (position.Y > AreaSize || position.Y < -AreaSize)
                    {
                        velocity.Y = -velocity.Y;
                    }

                    if (position.Z > AreaSize || position.Z < -AreaSize)
                    {
                        velocity.Z = -velocity.Z;
                    }

                    position += velocity * dt;

                    rotation *=
                        Quaternion.RotationX(rotVelocity.X * dt) *
                        Quaternion.RotationY(rotVelocity.Y * dt) *
                        Quaternion.RotationZ(rotVelocity.Z * dt);

                    color.R = (byte)(((position.X / AreaSize) + 1f) / 2.0f * 255.0f);
                    color.G = (byte)(((position.Y / AreaSize) + 1f) / 2.0f * 255.0f);
                    color.B = (byte)(((position.Z / AreaSize) + 1f) / 2.0f * 255.0f);
                    color.A = 255;

                });
            }

            int currentShape = 0;

            for (int i = 0; i < currentNumPrimitives; ++i)
            {

                ref var position = ref primitivePositions.Items[i];
                ref var rotation = ref primitiveRotations.Items[i];
                ref var velocity = ref primitiveVelocities.Items[i];
                ref var rotVelocity = ref primitiveRotVelocities.Items[i];
                ref var color = ref primitiveColors.Items[i];

                switch (mode)
                {
                    case CurRenderMode.All:
                        switch (currentShape++)
                        {
                            case 0: // sphere
                                DebugDraw.DrawSphere(position, 0.5f, color, depthTest: useDepthTesting, solid: !useWireframe);
                                break;
                            case 1: // cube
                                DebugDraw.DrawCube(position, new Vector3(1, 1, 1), rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                                break;
                            case 2: // capsule
                                DebugDraw.DrawCapsule(position, 1.0f, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                                break;
                            case 3: // cylinder
                                DebugDraw.DrawCylinder(position, 1.0f, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                                break;
                            case 4: // cone
                                DebugDraw.DrawCone(position, 1.0f, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                                break;
                            case 5: // ray
                                DebugDraw.DrawRay(position, velocity, color, depthTest: useDepthTesting);
                                break;
                            case 6: // quad
                                DebugDraw.DrawQuad(position, new Vector2(1.0f), rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                                break;
                            case 7: // circle
                                DebugDraw.DrawCircle(position, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                                currentShape = 0;
                                break;
                            default:
                                break;
                        }
                        break;
                    case CurRenderMode.Quad:
                        DebugDraw.DrawQuad(position, new Vector2(1.0f), rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.Circle:
                        DebugDraw.DrawCircle(position, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.Sphere:
                        DebugDraw.DrawSphere(position, 0.5f, color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.Cube:
                        DebugDraw.DrawCube(position, new Vector3(1, 1, 1), rotation, color, depthTest: useDepthTesting, solid: !useWireframe && i % 2 == 0);
                        break;
                    case CurRenderMode.Capsule:
                        DebugDraw.DrawCapsule(position, 1.0f, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.Cylinder:
                        DebugDraw.DrawCylinder(position, 1.0f, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.Cone:
                        DebugDraw.DrawCone(position, 1.0f, 0.5f, rotation, color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.Ray:
                        DebugDraw.DrawRay(position, velocity, color, depthTest: useDepthTesting);
                        break;
                    case CurRenderMode.Arrow:
                        DebugDraw.DrawArrow(position, velocity, color: color, depthTest: useDepthTesting, solid: !useWireframe);
                        break;
                    case CurRenderMode.None:
                        break;
                }
            }

            // CUBE OF ORIGIN!!
            DebugDraw.DrawCube(new Vector3(0, 0, 0), new Vector3(1.0f, 1.0f, 1.0f), color: Color.White);
            DebugDraw.DrawBounds(new Vector3(-5, 0, -5), new Vector3(5, 5, 5), color: Color.White);
            DebugDraw.DrawBounds(new Vector3(-AreaSize), new Vector3(AreaSize), color: Color.HotPink);

            if (Input.IsMouseButtonPressed(Xenko.Input.MouseButton.Left))
            {
                var clickPos = Input.MousePosition;
                var result = Utils.ScreenPositionToWorldPositionRaycast(clickPos, CurrentCamera, this.GetSimulation());
                if (result.Succeeded)
                {
                    var cameraWorldPos = CurrentCamera.Entity.Transform.WorldMatrix.TranslationVector;
                    var cameraWorldUp = CurrentCamera.Entity.Transform.WorldMatrix.Up;
                    var cameraWorldNormal = Vector3.Normalize(result.Point - cameraWorldPos);
                    DebugDraw.DrawLine(cameraWorldPos + (cameraWorldNormal * -2.0f) + (cameraWorldUp * (-0.125f / 4.0f)), result.Point, color: Color.HotPink, duration: 5.0f);
                    DebugDraw.DrawArrow(result.Point, result.Normal, coneHeight: 0.25f, coneRadius: 0.125f, color: Color.HotPink, duration: 5.0f);
                    DebugDraw.DrawArrow(result.Point, Vector3.Reflect(result.Point - cameraWorldPos, result.Normal), coneHeight: 0.25f, coneRadius: 0.125f, color: Color.LimeGreen, duration: 5.0f);
                }
            }

            DebugDraw.DrawCone(new Vector3(0, 0.5f, 0), 2.0f, 0.5f, color: Color.HotPink);

        }

    }

}
