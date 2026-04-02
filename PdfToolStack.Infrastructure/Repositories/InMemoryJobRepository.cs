using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Interfaces;

namespace PdfToolStack.Infrastructure.Repositories
{
    public class InMemoryJobRepository : IJobRepository
    {
        private static readonly Dictionary<Guid, PdfJob> Jobs = new();

        public Task<PdfJob> CreateAsync(PdfJob job)
        {
            Jobs[job.Id] = job;
            return Task.FromResult(job);
        }

        public Task<PdfJob?> GetByIdAsync(Guid id)
        {
            Jobs.TryGetValue(id, out var job);
            return Task.FromResult(job);
        }

        public Task UpdateAsync(PdfJob job)
        {
            Jobs[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id)
        {
            Jobs.Remove(id);
            return Task.CompletedTask;
        }
    }
}