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
    private InterfaceType _currentInterface = InterfaceType.Expert;
    
    public InterfaceType CurrentInterface => _currentInterface;
    public bool IsExpertInterface => _currentInterface == InterfaceType.Expert;
    public bool IsLearningInterface => _currentInterface == InterfaceType.Learning;
    
    public void SetContext(string currentPath)
    {
        if (currentPath.StartsWith("/learn", StringComparison.OrdinalIgnoreCase))
        {
            _currentInterface = InterfaceType.Learning;
        }
        else
        {
            _currentInterface = InterfaceType.Expert;
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
