using System;

namespace Hotel.Runtime
{
    public delegate bool SaveRunAttempt(GameRunState state, out string error);

    public static class SaveAndQuitFlow
    {
        private const string NoActiveRunMessage = "Cannot save and quit: no active run to save.";

        public static bool Execute(
            bool pauseMenuOpen,
            GameRunState runState,
            SaveRunAttempt trySave,
            Action onPauseRestored,
            Action onQuit,
            Action<string> onError)
        {
            if (!pauseMenuOpen)
                return false;

            if (runState == null)
            {
                onError?.Invoke(NoActiveRunMessage);
                return false;
            }

            if (!trySave(runState, out string error))
            {
                onError?.Invoke(string.IsNullOrEmpty(error)
                    ? "Cannot save and quit: save failed."
                    : "Cannot save and quit: " + error);
                return false;
            }

            onPauseRestored?.Invoke();
            onQuit?.Invoke();
            return true;
        }
    }
}
