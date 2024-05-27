namespace LocalizeFromSourceLib
{
    /// <summary>
    ///   Decorate a method or class with this if there are a lot of culture-invariant strings and very, very few localizable strings.
    ///   Basically if the risk of missing a localizable string is far outweighed by the bother of all the false-positives
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false)]
    public class NoStrictAttribute
        : Attribute
    {
    }
}
