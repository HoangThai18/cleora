using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Cleora.App.Platform
{
    /// <summary>
    /// One Cleora per user session. A second launch asks the running one to show its window (a broadcast message both
    /// copies know by name) and exits.
    /// </summary>
    internal sealed class SingleInstance : IDisposable
    {
        public static readonly int ShowMessage = RegisterWindowMessage("Cleora.ShowMainWindow");
        private const int BroadcastHandle = 0xFFFF;
        private readonly Mutex _mutex;

        private SingleInstance(Mutex mutex) => _mutex = mutex;

        /// <summary>
        /// <paramref name="wait"/>: how long to wait for a running copy to quit first (the copy that just restarted
        /// Cleora as administrator is still closing).
        /// </summary>
        public static SingleInstance? TryAcquire(TimeSpan wait = default)
        {
            var mutex = new Mutex(initiallyOwned: true, @"Local\Cleora.SingleInstance", out var createdNew);
            if (createdNew)
            {
                return new SingleInstance(mutex);
            }

            try
            {
                if (wait > TimeSpan.Zero && mutex.WaitOne(wait))
                {
                    return new SingleInstance(mutex);
                }
            }
            catch (AbandonedMutexException)
            {
                return new SingleInstance(mutex);
            }

            mutex.Dispose();
            PostMessage(new IntPtr(BroadcastHandle), ShowMessage, IntPtr.Zero, IntPtr.Zero);
            return null;
        }

        public void Dispose()
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
