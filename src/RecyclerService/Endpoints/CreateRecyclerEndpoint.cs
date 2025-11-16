using FastEndpoints;
using RecyclerService.Data;
using RecyclerService.Models;

namespace RecyclerService.Endpoints;

public class CreateRecyclerEndpoint : Endpoint<CreateRecyclerEndpoint.Request, CreateRecyclerEndpoint.Response>
{
    private readonly RecyclerDbContext _db;

    public CreateRecyclerEndpoint(RecyclerDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Verbs("POST");
        Routes("/recyclers");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var entity = new Recycler
        {
            Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
            Name = req.Name,
            Capacity = req.Capacity,
            CurrentLoad = 0,
            Location = req.Location,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Recyclers.Add(entity);
        await _db.SaveChangesAsync(ct);

        await Send.ResultAsync(TypedResults.Created($"/recyclers/{entity.Id}", new Response
        {
            Id = entity.Id,
            Name = entity.Name,
            Capacity = entity.Capacity,
            CurrentLoad = entity.CurrentLoad,
            Location = entity.Location
        }));
    }

    public record Request
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int Capacity { get; set; }
        public string? Location { get; set; }
    }

    public new record Response
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int Capacity { get; set; }
        public int CurrentLoad { get; set; }
        public string? Location { get; set; }
    }
}