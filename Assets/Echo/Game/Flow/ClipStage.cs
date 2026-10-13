namespace Echo.Game.Flow
{
    /// <summary>Where the clip editor is (a user revision): closed, scrubbing, previewing, recording or saving.</summary>
    public enum ClipStage
    {
        None,
        Editing,
        Previewing,
        Recording,
        Saving,
    }
}
