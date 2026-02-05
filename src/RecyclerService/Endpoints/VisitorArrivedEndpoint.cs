﻿using System.ComponentModel.DataAnnotations;
using FastEndpoints;
using FluentValidation;
using RecyclerService.Models;
using RecyclerService.Services;

namespace RecyclerService.Endpoints;

public class VisitorArrivedEndpoint : Endpoint<VisitorArrivedEndpoint.Request, VisitorArrivedEndpoint.VisitorResponse>
{
    private readonly IRecyclerService _service;

    public VisitorArrivedEndpoint(IRecyclerService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Verbs("POST");
        Routes("/recyclers/{id:guid}/visitors");
        AllowAnonymous();
        Options(x => x.WithTags("Recycler"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var recyclerId = Route<Guid>("id");
        var visitor = new Visitor();
        visitor.SetBottleCounts(BuildBottleCounts(req));
        visitor.VisitorType = req.VisitorType;
        try
        {
            var recycler = await _service.VisitorArrivedAsync(recyclerId, visitor, ct);
            await Send.ResultAsync(TypedResults.Ok(new VisitorResponse { RecyclerId = recycler.Id, CurrentLoad = recycler.CurrentLoad, Capacity = recycler.Capacity }));
        }
        catch (KeyNotFoundException)
        {
            ThrowError("Recycler not found", 404);
        }
    }

    private static Dictionary<string, int> BuildBottleCounts(Request req)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (req.BottleCounts is { Count: > 0 })
        {
            foreach (var kv in req.BottleCounts)
            {
                if (kv.Value > 0 && !string.IsNullOrWhiteSpace(kv.Key))
                {
                    counts[kv.Key.Trim().ToLowerInvariant()] = kv.Value;
                }
            }
        }

        if (counts.Count == 0)
        {
            AddIfPositive(counts, "glass", req.Glass);
            AddIfPositive(counts, "metal", req.Metal);
            AddIfPositive(counts, "plastic", req.Plastic);
        }

        if (counts.Count == 0 && req.Bottles > 0)
        {
            counts["regular"] = req.Bottles;
        }

        return counts;
    }

    private static void AddIfPositive(Dictionary<string, int> counts, string key, int? value)
    {
        if (value.HasValue && value.Value > 0)
        {
            counts[key] = value.Value;
        }
    }

    public record Request
    {
        public int Bottles { get; set; }

        public string? VisitorType { get; set; }

        public int? Glass { get; set; }

        public int? Metal { get; set; }

        public int? Plastic { get; set; }

        public Dictionary<string, int>? BottleCounts { get; set; }
    }

    public record VisitorResponse
    {
        public Guid RecyclerId { get; set; }
        public int CurrentLoad { get; set; }
        public int Capacity { get; set; }
    }

    public class RequestValidator : Validator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x).Must(HasBottleCounts)
                .WithMessage("Bottles must be greater than 0 or bottle counts must be provided");
        }

        private static bool HasBottleCounts(Request req)
        {
            if (req.Bottles > 0)
            {
                return true;
            }

            if (req.Glass.GetValueOrDefault() > 0 || req.Metal.GetValueOrDefault() > 0 || req.Plastic.GetValueOrDefault() > 0)
            {
                return true;
            }

            return req.BottleCounts?.Values.Any(v => v > 0) == true;
        }
    }
}