namespace EchoCloud
{
    /// <summary>An answer that can carry an error code instead of a result.</summary>
    public interface IFailable
    {
        string Error { get; set; }
    }
}
