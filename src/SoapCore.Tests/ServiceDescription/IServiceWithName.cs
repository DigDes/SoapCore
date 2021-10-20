using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests
{
	[ServiceContract(Name="MyServiceWithName")]
	internal interface IServiceWithName
	{
	}
}
