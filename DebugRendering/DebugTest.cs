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

        const int CHANGE_PER_SECOND = 8192 + 2048;
        const int INITIAL_NUM_BOXES = 32768;
        const int AREA_SIZE = 64;
        
        [DataMemberIgnore]
        DebugSystem debugSystem;

        int minNumberOfBoxes = 0;
        int maxNumberOfBoxes = INITIAL_NUM_BOXES * 10;
        int currentNumBoxes = INITIAL_NUM_BOXES;

        FastList<Vector3> boxPositions = new FastList<Vector3>(INITIAL_NUM_BOXES);
        FastList<Quaternion> boxRotations = new FastList<Quaternion>(INITIAL_NUM_BOXES);
        FastList<Vector3> boxVelocities = new FastList<Vector3>(INITIAL_NUM_BOXES);
        FastList<Vector3> boxRotVelocities = new FastList<Vector3>(INITIAL_NUM_BOXES);
        FastList<Color> boxColors = new FastList<Color>(INITIAL_NUM_BOXES);

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

                var randX = random.Next(-AREA_SIZE, AREA_SIZE);
                var randY = random.Next(-AREA_SIZE, AREA_SIZE);
                var randZ = random.Next(-AREA_SIZE, AREA_SIZE);

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
            debugSystem.TailSize = currentNumBoxes;

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
            
            var newAmountOfBoxes = Clamp(currentNumBoxes + (int)(Input.MouseWheelDelta * CHANGE_PER_SECOND * dt), minNumberOfBoxes, maxNumberOfBoxes);

            if (newAmountOfBoxes > currentNumBoxes)
            {
                InitializeBoxes(currentNumBoxes, newAmountOfBoxes);
                debugSystem.TailSize = newAmountOfBoxes;
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

                if (position.X > AREA_SIZE || position.X < -AREA_SIZE)
                {
                    velocity.X = -velocity.X;
                }

                if (position.Y > AREA_SIZE || position.Y < -AREA_SIZE)
                {
                    velocity.Y = -velocity.Y;
                }

                if (position.Z > AREA_SIZE || position.Z < -AREA_SIZE)
                {
                    velocity.Z = -velocity.Z;
                }

                position += velocity * dt;

                rotation *=
                    Quaternion.RotationX(rotVelocity.X * dt) *
                    Quaternion.RotationY(rotVelocity.Y * dt) *
                    Quaternion.RotationZ(rotVelocity.Z * dt);

                color.R = (byte)((((position.X / AREA_SIZE) + 1f) / 2.0f) * 255.0f);
                color.G = (byte)((((position.Y / AREA_SIZE) + 1f) / 2.0f) * 255.0f);
                color.B = (byte)((((position.Z / AREA_SIZE) + 1f) / 2.0f) * 255.0f);

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
                        debugSystem.DrawBox(boxPositions.Items[i], boxRotations.Items[i], new Vector3(1), boxColors.Items[i]);
                        break;
                    case 2: // capsule
                        debugSystem.DrawCapsule(boxPositions.Items[i], boxRotations.Items[i], boxColors.Items[i]);
                        break;
                    case 3: // cylinder
                        debugSystem.DrawCylinder(boxPositions.Items[i], boxRotations.Items[i], boxColors.Items[i]);
                        break;
                    case 4: // cone
                        debugSystem.DrawCone(boxPositions.Items[i], boxRotations.Items[i], boxColors.Items[i]);
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
