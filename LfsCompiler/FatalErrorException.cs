namespace LocalizeFromSource
{
    /// <summary>
    ///   Thrown when something completely prevents compilation.
    /// </summary>
    internal class FatalErrorException : Exception
    {
        public FatalErrorException(string message, int errorCode, Exception? innerException = null)
            : base(message, innerException)
        {
            this.ErrorCode = errorCode;
        }

        public int ErrorCode { get; }
    }
}
