namespace SilverPay.GenEnum
{
    /// <summary>
    /// MessageQueue_Status_Enum枚举
    /// </summary>
    public sealed class MsgStatus
    {
        /// <summary>
        /// Created: 1
        /// </summary>
        public static readonly short Created = 1;
        /// <summary>
        /// Retrying: 2
        /// </summary>
        public static readonly short Retrying = 2;
        /// <summary>
        /// ArrivedBroker: 3
        /// </summary>
        public static readonly short ArrivedBroker = 3;
        /// <summary>
        /// ArrivedConsumer: 4
        /// </summary>
        public static readonly short ArrivedConsumer = 4;
        /// <summary>
        /// Exception: 5
        /// </summary>
        public static readonly short Exception = 5;
        /// <summary>
        /// Processed: 6
        /// </summary>
        public static readonly short Processed = 6;
    }
}
