namespace ReturnFlow.Application;

public interface IDemoSessionIdGenerator
{
    string Create();
}

public sealed class GuidDemoSessionIdGenerator : IDemoSessionIdGenerator
{
    public string Create() => Guid.NewGuid().ToString("N");
}
