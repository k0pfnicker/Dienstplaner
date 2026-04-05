using Dienstplanner.Domain.Models;

namespace Dienstplanner.Application.Interfaces;

public interface IScheduleImporter
{
    Task<ImportResult> ImportAsync(string filePath);
}
