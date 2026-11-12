namespace Identity.Repositories.Data;

public interface IOutbox
{
    void Add<T>(string stream, T payload) where T : class;
}
