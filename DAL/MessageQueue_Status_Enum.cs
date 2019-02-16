namespace SilverPay.GenEnum
{
    /// <summary>
    /// MessageQueue_Status_Enum枚举
    /// </summary>
    public enum MessageQueue_Status_Enum
	{
		Created = 1,
		Retrying = 2,
		ArrivedBroker = 3,
		ArrivedConsumer = 4,
		Exception = 5,
		Processed = 6
	}
}
