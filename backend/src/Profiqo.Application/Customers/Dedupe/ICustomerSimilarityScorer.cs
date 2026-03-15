// Path: backend/src/Profiqo.Application/Customers/Dedupe/ICustomerSimilarityScorer.cs
namespace Profiqo.Application.Customers.Dedupe;

public interface ICustomerSimilarityScorer
{
    Task<double> ScoreAsync(CustomerDuplicateCandidateDto a, CustomerDuplicateCandidateDto b, CancellationToken ct);
}