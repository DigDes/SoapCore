using System.ServiceModel;

namespace SoapCore.Tests.Wsdl.Services
{
	[ServiceContract]
	public interface IOperationContractFieldMembersServiceWrapped
	{
		[OperationContract]
		TypeWithFields Method(TypeWithFieldsWrapped argument);
	}

	public class OperationContractFieldMembersServiceWrapped : IOperationContractFieldMembersServiceWrapped
	{
		public TypeWithFields Method(TypeWithFieldsWrapped argument)
		{
			return new TypeWithFields();
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Required for test")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Tightly coupled")]
	[MessageContract(IsWrapped = true)]
	public class TypeWithFieldsWrapped
	{
		[MessageBodyMember]
		public TypeWithFields TypeWithFields;
	}
}
