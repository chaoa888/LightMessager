namespace LightMessager.Repository
{
    public sealed class MessageState
    {
        /// <summary>
        /// Created: 1
        /// </summary>
        public const int Created = 1;
        /// <summary>
        /// Persistent: 2
        /// </summary>
        public const int Persistent = 2;
        /// <summary>
        /// Consumed: 3
        /// </summary>
        public const int Consumed = 3;
        /// <summary>
        /// Error: 4
        /// </summary>
        public const int Error = 4;
        /// <summary>
        /// Error_Unroutable: 5
        /// </summary>
        public const int Error_Unroutable = 5;
    }
}
