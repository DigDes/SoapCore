using System.ServiceModel;
using System.Text;

namespace SoapCore.Tests.MessageContract.Models
{
	[ServiceContract(Namespace = "http://tempuri.org")]
	public interface IArrayOfArrayOfByteService
	{
		[OperationContract]
		string ArrayOfArrayOfByteMethod(byte[][] arrayOfArrayOfByteParam);
	}

	public class ArrayOfArrayOfByteService : IArrayOfArrayOfByteService
	{
		public string ArrayOfArrayOfByteMethod(byte[][] arrayOfArrayOfByteParam)
		{
			var ret = new StringBuilder();
			ret.Append("[");
			foreach (var array in arrayOfArrayOfByteParam)
			{
				ret.Append("[");
				ret.Append(string.Join(",", array));
				ret.Append("]");
			}

			ret.Append("]");
			return ret.ToString();
		}
	}
}
