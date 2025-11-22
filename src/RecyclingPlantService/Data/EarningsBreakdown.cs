namespace RecyclingPlantService.Data;

public class EarningsBreakdown
{
    public decimal GlassEarnings { get; set; }
    public decimal MetalEarnings { get; set; }
    public decimal PlasticEarnings { get; set; }
    public int GlassCount { get; set; }
    public int MetalCount { get; set; }
    public int PlasticCount { get; set; }
}