using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IAttributeService
	{
		[OperationContract]
		AttributeType Method();
	}

	public class AttributeService : IAttributeService
	{
		public AttributeType Method()
		{
			throw new NotImplementedException();
		}
	}
}
