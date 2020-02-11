using System.ServiceModel;
using System.Xml;

namespace SoapCore
{
	public static class EnvelopeVersionExtentions
	{
		public static string Namespace(this EnvelopeVersion envelopeVersion)
		{
			if (envelopeVersion == EnvelopeVersion.Soap11)
			{
				return Namespaces.SOAP11_ENVELOPE_NS;
			}

			return Namespaces.SOAP12_ENVELOPE_NS;
		}

		public static string NamespacePrefix(this EnvelopeVersion envelopeVersion, XmlNamespaceManager namespaces)
		{
			string prefix;
			if (envelopeVersion == EnvelopeVersion.Soap11)
			{
				prefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(namespaces, "s", Namespaces.SOAP11_ENVELOPE_NS);
				return prefix;
			}

			prefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(namespaces, "s", Namespaces.SOAP12_ENVELOPE_NS);
			return prefix;
		}
	}
}
