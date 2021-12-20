namespace SoapCore.Tests.Serialization.Models.Xml
{
    [System.ServiceModel.MessageContract(WrapperName = "TestMessageContractWithWithRpcStyleResponse", WrapperNamespace = "http://xmlelement-namespace/", IsWrapped = true)]
    public class MessageContractResponseRpcStyle
    {
        [System.ServiceModel.MessageBodyMember(Namespace = "", Order = 0)]
        public int Result { get; set; }

        [System.ServiceModel.MessageBodyMember(Namespace = "", Order = 1)]
        public string Message { get; set; }
    }
}
