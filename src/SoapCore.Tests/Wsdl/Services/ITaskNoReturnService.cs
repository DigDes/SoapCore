using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface ITaskNoReturnService
	{
		[OperationContract]
		Task TaskNoResultMethod();
	}

	public class TaskNoReturnService : ITaskNoReturnService
	{
		public Task TaskNoResultMethod()
		{
			throw new NotImplementedException();
		}
	}
}
