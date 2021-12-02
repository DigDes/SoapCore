using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org")]
	public interface IArrayOfIntService
	{
		[OperationContract]
		int[] ArrayOfIntMethod(int[] arrayOfIntParam);

		[OperationContract]
		int IntMethod(int intParam);
	}

	public class ArrayOfIntService : IArrayOfIntService
	{
		public int[] ArrayOfIntMethod(int[] arrayOfIntParam)
		{
			return arrayOfIntParam;
		}

		public int IntMethod(int intParam)
		{
			return intParam;
		}
	}
}
