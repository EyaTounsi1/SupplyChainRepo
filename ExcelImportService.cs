using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using PartTracker.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PartTracker;

public class ExcelImportService : IExcelImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExcelImportService> _logger;
    private readonly IWebHostEnvironment _env;

    public ExcelImportService(AppDbContext db, ILogger<ExcelImportService> logger, IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    public async Task ImportChangeLogAsync(CancellationToken cancellationToken = default)
    {
        var excelFilePath = @"C:\Users\ETOUNSI\OneDrive - Volvo Cars\Desktop\Supply Chain\Power Bi\Activity Form Answers\Activity.xlsx";

        if (!File.Exists(excelFilePath))
        {
            _logger.LogWarning("Excel file not found at {Path}", excelFilePath);
            return;
        }

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage(new FileInfo(excelFilePath));
        var ws = package.Workbook.Worksheets[0]; // First sheet

        int currentRow = 2; // assuming row 1 is headers
        var now = DateTime.UtcNow;
        int premiumTodayCount = 0;

        while (true)
        {
            var idCell = ws.Cells[currentRow, 1].Text;
            if (string.IsNullOrWhiteSpace(idCell)) break;

            var entry = new ChangeLogEntry
            {
                ExcelId = idCell,
                StartTime = ParseDate(ws.Cells[currentRow, 2].Text),
                CompletionTime = ParseDate(ws.Cells[currentRow, 3].Text),
                Email = ws.Cells[currentRow, 4].Text,
                Name = ws.Cells[currentRow, 5].Text,
                Language = ws.Cells[currentRow, 6].Text,
                PremiumBooking = ws.Cells[currentRow, 7].Text,
                AftermarketCollectionOrSpeedUp = ws.Cells[currentRow, 8].Text,
                ToNumber = ws.Cells[currentRow, 9].Text,
                MfgCode = ws.Cells[currentRow, 10].Text,
                ShpCode = ws.Cells[currentRow, 11].Text,
                Reasons1 = ws.Cells[currentRow, 12].Text,
                Reasons = ws.Cells[currentRow, 13].Text,
                Comment = ws.Cells[currentRow, 14].Text,
                Pn = ws.Cells[currentRow, 15].Text,
                Reasons2 = ws.Cells[currentRow, 16].Text,
                Quantity = ws.Cells[currentRow, 17].Text,
                CollectedBy = ws.Cells[currentRow, 18].Text,
                Comment1 = ws.Cells[currentRow, 19].Text,
                ToNumber1 = ws.Cells[currentRow, 20].Text,
                PartNumber = ws.Cells[currentRow, 21].Text,
                Reasons3 = ws.Cells[currentRow, 22].Text,
                MfgCode1 = ws.Cells[currentRow, 23].Text,
                Comment2 = ws.Cells[currentRow, 24].Text,
                LastUpdated = now
            };

            if (string.Equals(entry.PremiumBooking, "premium booking", StringComparison.OrdinalIgnoreCase) && entry.CompletionTime.HasValue && entry.CompletionTime.Value.Date == DateTime.Today)
            {
                premiumTodayCount++;
            }

            // Use ExcelId as unique key
            var existing = await _db.ChangeLogEntries
                .FirstOrDefaultAsync(x => x.ExcelId == entry.ExcelId, cancellationToken);

            if (existing == null)
            {
                _db.ChangeLogEntries.Add(entry);
            }
            else
            {
                // Update existing
                existing.StartTime = entry.StartTime;
                existing.CompletionTime = entry.CompletionTime;
                existing.Email = entry.Email;
                existing.Name = entry.Name;
                existing.Language = entry.Language;
                existing.PremiumBooking = entry.PremiumBooking;
                existing.AftermarketCollectionOrSpeedUp = entry.AftermarketCollectionOrSpeedUp;
                existing.ToNumber = entry.ToNumber;
                existing.MfgCode = entry.MfgCode;
                existing.ShpCode = entry.ShpCode;
                existing.Reasons1 = entry.Reasons1;
                existing.Reasons = entry.Reasons;
                existing.Comment = entry.Comment;
                existing.Pn = entry.Pn;
                existing.Reasons2 = entry.Reasons2;
                existing.Quantity = entry.Quantity;
                existing.CollectedBy = entry.CollectedBy;
                existing.Comment1 = entry.Comment1;
                existing.ToNumber1 = entry.ToNumber1;
                existing.PartNumber = entry.PartNumber;
                existing.Reasons3 = entry.Reasons3;
                existing.MfgCode1 = entry.MfgCode1;
                existing.Comment2 = entry.Comment2;
                existing.LastUpdated = now;
            }

            currentRow++;
        }

        _logger.LogInformation("Premium bookings in file for today: {Count}", premiumTodayCount);

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Excel sync completed at {Time}", now);
    }

    private DateTime? ParseDate(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;
        // Normalize spaces
        dateString = System.Text.RegularExpressions.Regex.Replace(dateString.Trim(), @"\s+", " ");
        if (DateTime.TryParse(dateString, out var date)) return date;
        return null;
    }
}