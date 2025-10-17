namespace FlowTime.UI.Services.Interface;

public interface IInterfaceContextService
{
    InterfaceType CurrentInterface { get; }
    bool IsExpertInterface { get; }
    bool IsLearningInterface { get; }
    void SetContext(string currentPath);
    string GetTransitionUrl(InterfaceType targetInterface);
}

public enum InterfaceType
{
    Expert,
    Learning
}

public class InterfaceContextService : IInterfaceContextService
{
    private InterfaceType currentInterface = InterfaceType.Expert;
    
    public InterfaceType CurrentInterface => currentInterface;
    public bool IsExpertInterface => currentInterface == InterfaceType.Expert;
    public bool IsLearningInterface => currentInterface == InterfaceType.Learning;
    
    public void SetContext(string currentPath)
    {
        if (currentPath.StartsWith("/learn", StringComparison.OrdinalIgnoreCase))
        {
            currentInterface = InterfaceType.Learning;
        }
        else
        {
            currentInterface = InterfaceType.Expert;
        }
    }
    
    public string GetTransitionUrl(InterfaceType targetInterface)
    {
        return targetInterface switch
        {
            InterfaceType.Expert => "/app",
            InterfaceType.Learning => "/learn",
            _ => "/"
        };
    }
}
