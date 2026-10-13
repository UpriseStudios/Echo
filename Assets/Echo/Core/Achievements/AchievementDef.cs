namespace Echo.Core.Achievements
{
    /// <summary>One achievement (skins addendum): progress counts up to Target; hidden ones show once started.</summary>
    public sealed class AchievementDef
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly bool Hidden;
        public readonly int Target;

        public AchievementDef(string id, string title, string description, int target = 1, bool hidden = false)
        {
            Id = id;
            Title = title;
            Description = description;
            Target = target;
            Hidden = hidden;
        }
    }
}
