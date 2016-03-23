using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HolographicStudio
{
    /// <summary>
    /// Simple application using SharpDX.Toolkit.
    /// </summary>
    class Program 
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
#if NETFX_CORE
                [MTAThread]
#else
        [STAThread]
#endif
        static void Main()
        {
            try
            {
                using (var game = new HoloStudio())
                {
                    game.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal exception occured");
            }
        }
    }
}
