using Microsoft.EntityFrameworkCore;
using PdfToolkit.Infrastructure.Data;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Interfaces;


namespace PdfToolkit.Infrastructure.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly AppDbContext _context;

        public JobRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PdfJob> CreateAsync(PdfJob job)
        {
            await _context.PdfJobs.AddAsync(job);
            await _context.SaveChangesAsync();
            return job;
        }

        public async Task<PdfJob?> GetByIdAsync(Guid id)
        {
            return await _context.PdfJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == id);
        }

        public async Task UpdateAsync(PdfJob job)
        {
            _context.PdfJobs.Update(job);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var job = await _context.PdfJobs
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job != null)
            {
                _context.PdfJobs.Remove(job);
                await _context.SaveChangesAsync();
            }
        }
    }
}
