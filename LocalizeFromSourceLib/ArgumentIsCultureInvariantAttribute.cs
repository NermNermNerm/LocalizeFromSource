namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   Decorate a method with this if its string arguments are always culture-invariant.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ArgumentIsCultureInvariantAttribute
        : Attribute
    {
    }
}
