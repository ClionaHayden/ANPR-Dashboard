using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnprDashboardServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecordHistoryController : ControllerBase
    {
        private readonly AppDbContext _db;

        public RecordHistoryController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(string? search = null)
        {
            var query = _db.Detections.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim().ToLower();
                query = query.Where(d => d.Plate.ToLower().Contains(trimmedSearch));
            }

            var history = await query
                .OrderByDescending(d => d.Timestamp)
                .ToListAsync();

            return Ok(history);
        }
    }
}
