﻿using System;
using System.Linq;

using Xenko.Core;
using Xenko.Core.Collections;
using Xenko.Core.Mathematics;
using Xenko.Core.Threading;
using Xenko.Engine;

namespace DebugRendering 
{

    public class DebugTest : SyncScript
    {

        enum CurRenderMode : byte
        {
            All = 0,
            Sphere,
            Cube,
            Capsule,
            Cylinder,
            Cone
        }

        const int ChangePerSecond = 8192 + 2048;
        const int InitialNumPrimitives = 32768;
        const int AreaSize = 64;
        
        [DataMemberIgnore]
        DebugSystem debugSystem;

        int minNumberofPrimitives = 0;
        int maxNumbeOfPrimitives = InitialNumPrimitives * 10;
        int currentNumPrimitives = InitialNumPrimitives;
        CurRenderMode mode = CurRenderMode.All;

        FastList<Vector3> primitivePositions = new FastList<Vector3>(InitialNumPrimitives);
        FastList<Quaternion> primitiveRotations = new FastList<Quaternion>(InitialNumPrimitives);
        FastList<Vector3> primitiveVelocities = new FastList<Vector3>(InitialNumPrimitives);
        FastList<Vector3> primitiveRotVelocities = new FastList<Vector3>(InitialNumPrimitives);
        FastList<Color> primitiveColors = new FastList<Color>(InitialNumPrimitives);

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

            }
        }

        public override void Start() {

            // keep DebugText visible in release builds too
            DebugText.Visible = true;

            debugSystem = new DebugSystem(Services);
            debugSystem.PrimitiveRenderer = SceneSystem.GraphicsCompositor.RenderFeatures.OfType<DebugRenderFeature>().First();
            debugSystem.PrimitiveColor = Color.Green;
            debugSystem.NormalTailSize = currentNumPrimitives + 1;

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

        public override void Update() {

            var dt = (float)Game.UpdateTime.Elapsed.TotalSeconds;
            
            var newAmountOfBoxes = Clamp(currentNumPrimitives + (int)(Input.MouseWheelDelta * ChangePerSecond * dt), minNumberofPrimitives, maxNumbeOfPrimitives);

            if (Input.IsKeyPressed(Xenko.Input.Keys.LeftAlt))
            {
                mode = (CurRenderMode)(((int)mode + 1) % ((int)CurRenderMode.Cone + 1));
            }

            if (newAmountOfBoxes > currentNumPrimitives)
            {
                InitializePrimitives(currentNumPrimitives, newAmountOfBoxes);
                debugSystem.NormalTailSize = newAmountOfBoxes + 1;
                currentNumPrimitives = newAmountOfBoxes;
            }
            else
            {
                currentNumPrimitives = newAmountOfBoxes;
            }

            DebugText.Print($"Primitive Count: {currentNumPrimitives} (scrollwheel to adjust)", new Int2((int)Input.Mouse.SurfaceSize.X - 384, 32));
            DebugText.Print($" - Current Render Mode: {mode} (alt to switch)", new Int2((int)Input.Mouse.SurfaceSize.X - 384, 48));

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

                color.R = (byte)((((position.X / AreaSize) + 1f) / 2.0f) * 255.0f);
                color.G = (byte)((((position.Y / AreaSize) + 1f) / 2.0f) * 255.0f);
                color.B = (byte)((((position.Z / AreaSize) + 1f) / 2.0f) * 255.0f);

            });

            int currentShape = 0;
            var ds = debugSystem;

            for (int i = 0; i < currentNumPrimitives; ++i)
            {
                switch (mode)
                {
                    case CurRenderMode.All:
                        switch (currentShape++)
                        {
                            case 0: // sphere
                                debugSystem.DrawSphere(primitivePositions.Items[i], 0.5f, primitiveColors.Items[i]);
                                break;
                            case 1: // cube
                                debugSystem.DrawCube(primitivePositions.Items[i], new Vector3(1, 1, 1), primitiveRotations.Items[i], primitiveColors.Items[i]);
                                break;
                            case 2: // capsule
                                debugSystem.DrawCapsule(primitivePositions.Items[i], 1.0f, 0.5f, primitiveRotations.Items[i], primitiveColors.Items[i]);
                                break;
                            case 3: // cylinder
                                debugSystem.DrawCylinder(primitivePositions.Items[i], 1.0f, 0.5f, primitiveRotations.Items[i], primitiveColors.Items[i]);
                                break;
                            case 4: // cone
                                debugSystem.DrawCone(primitivePositions.Items[i], 1.0f, 0.5f, primitiveRotations.Items[i], primitiveColors.Items[i]);
                                currentShape = 0;
                                break;
                            default:
                                break;
                        }
                        break;
                    case CurRenderMode.Sphere:
                        debugSystem.DrawSphere(primitivePositions.Items[i], (float)Math.Sin(Game.PlayTime.TotalTime.TotalSeconds) + (float)Math.Cos(i), primitiveColors.Items[i]);
                        break;
                    case CurRenderMode.Cube:
                        debugSystem.DrawCube(primitivePositions.Items[i], new Vector3(1, 1, 1), primitiveRotations.Items[i], primitiveColors.Items[i]);
                        break;
                    case CurRenderMode.Capsule:
                        debugSystem.DrawCapsule(primitivePositions.Items[i], 1.0f, 0.5f, primitiveRotations.Items[i], primitiveColors.Items[i]);
                        break;
                    case CurRenderMode.Cylinder:
                        debugSystem.DrawCylinder(primitivePositions.Items[i], 1.0f, 0.5f, primitiveRotations.Items[i], primitiveColors.Items[i]);
                        break;
                    case CurRenderMode.Cone:
                        debugSystem.DrawCone(primitivePositions.Items[i], 1.0f, 0.5f, primitiveRotations.Items[i], primitiveColors.Items[i]);
                        break;
                }
            }

            // CUBE OF ORIGIN!!
            debugSystem.DrawCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1), Quaternion.Identity);

            debugSystem.Update(Game.UpdateTime);

        }

    }

}
