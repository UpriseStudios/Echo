using UnityEngine;

namespace Echo.Game.Flow
{
    /// <summary>
    /// Leaving the game (a user revision): an Exit button on the title and Android's back button. Apple does not
    /// allow an iOS app to quit itself, so there it is not offered. Everything is saved before the app closes.
    /// </summary>
    public sealed partial class EchoGame
    {
        /// <summary>Exit is offered everywhere but iOS (the editor shows it so it can be tried).</summary>
        static bool CanQuit => Application.platform != RuntimePlatform.IPhonePlayer;

        /// <summary>Set when the player confirmed Exit. Test hook: the editor cannot quit.</summary>
        public bool QuitRequested { get; private set; }

        void AskToQuit()
        {
            if (!CanQuit || State != GameState.Title || ModalOpen) return;
            Ask("LEAVE ECHO?", "Your progress is saved. Your ghosts will be waiting.", "Exit", "Stay", Quit, null);
        }

        void Quit()
        {
            QuitRequested = true;
            Save();
            Application.Quit();
        }

        // Back: closes an open card or menu, and on the title asks to leave.
        void OnBack()
        {
            if (ChallengeCardOpen)
            {
                CloseChallengeCard(_challengeNo);
                return;
            }
            if (_powerUpsView != null && _powerUpsView.IsOpen)
            {
                _powerUpsView.Hide();
                return;
            }
            if (State == GameState.Menu)
            {
                if (ReelsOpen && _reels.ReplaceMode) return;
                CloseMenu();
                return;
            }
            if (State == GameState.Title) AskToQuit();
        }
    }
}
