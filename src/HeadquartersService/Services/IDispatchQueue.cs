using HeadquartersService.Models;

namespace HeadquartersService.Services;

public interface IDispatchQueue
{
    void Enqueue(DispatchRequest req);
    bool TryDequeue(out DispatchRequest? req);
    IReadOnlyList<DispatchRequest> PeekAll();
    DispatchRequest? Get(Guid id);
    void Reset();
}