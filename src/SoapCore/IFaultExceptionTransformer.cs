using System;
using System.ServiceModel.Channels;

namespace SoapCore
{
	public interface IFaultExceptionTransformer
	{
		Message ProvideFault(Exception exception);
	}
}
