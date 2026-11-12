namespace UMLForge.Diagram.Service.Interfaces;

public interface IOutbox
{
    void Add<T>(string stream, T payload) where T : class;
}
