using System;
using System.Windows.Threading;

namespace AutomatedClashRunner.Utils
{
    public static class DispatcherUtils
    {
        /// <summary>
        /// Pumps the WPF dispatcher loop to allow UI rendering and progress updates
        /// during heavy synchronous operations on the main STA thread (ISS-032).
        /// </summary>
        public static void DoEvents()
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                dispatcher?.Invoke(() => { }, DispatcherPriority.Background);
            }
            catch
            {
                // Ignored to avoid crashing background pump
            }
        }
    }
}
