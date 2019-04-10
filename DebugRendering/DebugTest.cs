using System;
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

        const int ChangePerSecond = 8192 + 2048;
        const int InitialNumBoxes = 32768;
        const int AreaSize = 64;
        
        [DataMemberIgnore]
        DebugSystem debugSystem;

        int minNumberOfBoxes = 0;
        int maxNumberOfBoxes = InitialNumBoxes * 10;
        int currentNumBoxes = InitialNumBoxes;

        FastList<Vector3> boxPositions = new FastList<Vector3>(InitialNumBoxes);
        FastList<Quaternion> boxRotations = new FastList<Quaternion>(InitialNumBoxes);
        FastList<Vector3> boxVelocities = new FastList<Vector3>(InitialNumBoxes);
        FastList<Vector3> boxRotVelocities = new FastList<Vector3>(InitialNumBoxes);
        FastList<Color> boxColors = new FastList<Color>(InitialNumBoxes);

        private void InitializeBoxes(int from, int to)
        {

            var random = new Random();

            boxPositions.Resize(to, true);
            boxRotations.Resize(to, true);
            boxVelocities.Resize(to, true);
            boxRotVelocities.Resize(to, true);
            boxColors.Resize(to, true);

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

                boxPositions.Items[i] = new Vector3(randX, randY, randZ);
                boxRotations.Items[i] = Quaternion.Identity;
                boxVelocities.Items[i] = ballVel;
                boxRotVelocities.Items[i] = ballRotVel;

            }
        }

        public override void Start() {

            // keep DebugText visible in release builds too
            DebugText.Visible = true;

            debugSystem = new DebugSystem(Services);
            debugSystem.PrimitiveRenderer = SceneSystem.GraphicsCompositor.RenderFeatures.OfType<DebugRenderFeature>().First();
            debugSystem.PrimitiveColor = Color.Green;
            debugSystem.NormalTailSize = currentNumBoxes;

            InitializeBoxes(0, currentNumBoxes);

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
            
            var newAmountOfBoxes = Clamp(currentNumBoxes + (int)(Input.MouseWheelDelta * ChangePerSecond * dt), minNumberOfBoxes, maxNumberOfBoxes);

            if (newAmountOfBoxes > currentNumBoxes)
            {
                InitializeBoxes(currentNumBoxes, newAmountOfBoxes);
                debugSystem.NormalTailSize = newAmountOfBoxes;
                currentNumBoxes = newAmountOfBoxes;
            }
            else
            {
                currentNumBoxes = newAmountOfBoxes;
            }

            DebugText.Print($"Primitive Count: {currentNumBoxes} (scrollwheel to adjust)", new Int2((int)Input.Mouse.SurfaceSize.X - 384, 32));

            Dispatcher.For(0, currentNumBoxes, i =>
            {

                ref var position = ref boxPositions.Items[i];
                ref var velocity = ref boxVelocities.Items[i];
                ref var rotVelocity = ref boxRotVelocities.Items[i];
                ref var rotation = ref boxRotations.Items[i];
                ref var color = ref boxColors.Items[i];

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

            for (int i = 0; i < currentNumBoxes; ++i)
            {
                switch (currentShape++)
                {
                    case 0: // sphere
                        debugSystem.DrawSphere(boxPositions.Items[i], 0.5f, boxColors.Items[i]);
                        break;
                    case 1: // cube
                        debugSystem.DrawCube(boxPositions.Items[i], new Vector3(1, 1, 1), boxRotations.Items[i], boxColors.Items[i]);
                        break;
                    case 2: // capsule
                        debugSystem.DrawCapsule(boxPositions.Items[i], 1.0f, 0.5f, boxRotations.Items[i], boxColors.Items[i]);
                        break;
                    case 3: // cylinder
                        debugSystem.DrawCylinder(boxPositions.Items[i], 1.0f, 0.5f, boxRotations.Items[i], boxColors.Items[i]);
                        break;
                    case 4: // cone
                        debugSystem.DrawCone(boxPositions.Items[i], 1.0f, 0.5f, boxRotations.Items[i], boxColors.Items[i]);
                        currentShape = 0;
                        break;
                    default:
                        break;
                }
            }

            // debugSystem.DrawBoxes(boxPositions, boxRotations, new Vector3(1));
            debugSystem.Update(Game.UpdateTime);

        }

    }

}
