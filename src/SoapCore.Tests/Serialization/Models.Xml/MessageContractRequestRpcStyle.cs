namespace SoapCore.Tests.Serialization.Models.Xml
{
    [System.ServiceModel.MessageContract(WrapperName = "TestMessageContractWithWithRpcStyle", WrapperNamespace = "http://xmlelement-namespace/", IsWrapped = true)]
    public class MessageContractRequestRpcStyle
    {
        [System.ServiceModel.MessageBodyMember(Namespace = "", Order = 0)]
        public string StringParameter { get; set; }

        [System.ServiceModel.MessageBodyMember(Namespace = "", Order = 1)]
        public int IntParameter { get; set; }

        [System.ServiceModel.MessageBodyMember(Namespace = "", Order = 2)]
        public SampleEnum EnumParameter { get; set; }
    }
}
