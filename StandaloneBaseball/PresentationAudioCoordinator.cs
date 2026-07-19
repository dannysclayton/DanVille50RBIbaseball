#nullable enable annotations

using System;

namespace StandaloneBaseball
{
    internal static class PresentationAudioCoordinator
    {
        private static readonly object Sync = new object();
        private static LaunchSoundPlayer? _activeLoopOwner;

        public static void StartExclusiveLoop(LaunchSoundPlayer owner, Action startLoop)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(startLoop);

            lock (Sync)
            {
                if (!ReferenceEquals(_activeLoopOwner, owner))
                    _activeLoopOwner?.Stop();
                _activeLoopOwner = owner;
                startLoop();
            }
        }

        public static void Stop(LaunchSoundPlayer owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            lock (Sync)
            {
                owner.Stop();
                if (ReferenceEquals(_activeLoopOwner, owner))
                    _activeLoopOwner = null;
            }
        }

        internal static bool IsActiveOwnerForTests(LaunchSoundPlayer owner)
        {
            lock (Sync)
                return ReferenceEquals(_activeLoopOwner, owner);
        }

        internal static void ResetForTests()
        {
            lock (Sync)
            {
                _activeLoopOwner?.Stop();
                _activeLoopOwner = null;
            }
        }
    }
}
