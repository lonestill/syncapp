namespace Ecosystem.Contracts;

public interface IPlugin
{
    string GetName();
    void Execute();
}
