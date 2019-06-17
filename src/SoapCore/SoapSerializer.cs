namespace SoapCore
{
    public enum SoapSerializer
    {
        /// <summary>
        /// Client created from wsdl via Connected Services - Add Service Reference (see https://stackoverflow.com/a/2468182)
        /// </summary>
        XmlSerializer,

        /// <summary>
        /// Client created from interface via <see cref="System.ServiceModel.ChannelFactory" />
        /// </summary>
        DataContractSerializer,

		/// <summary>
		/// XmlSerializer like but more close to legacy WCF WSDL structure
		/// </summary>
		XmlWcfSerializer
	}
}
