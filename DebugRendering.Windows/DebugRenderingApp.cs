using Xenko.Engine;

namespace DebugRendering.Windows
{
    class DebugRenderingApp
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}
