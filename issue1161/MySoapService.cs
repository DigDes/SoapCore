using System.ServiceModel;

[ServiceContract]
public interface IMySoapService
{
    [OperationContract]
    MyMessage Process(MyMessage rq);
}

public class MySoapService : IMySoapService
{
    public MyMessage Process(MyMessage msg)
    {
        return msg;
    }

}
