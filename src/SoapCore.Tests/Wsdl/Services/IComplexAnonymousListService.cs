using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IComplexAnonymousListService
	{
		[OperationContract]
		List<ComplexTypeAnonymous> Test();
	}

	public class ComplexAnonymousListService : IComplexAnonymousListService
	{
		public List<ComplexTypeAnonymous> Test() => throw new NotImplementedException();
	}
}
