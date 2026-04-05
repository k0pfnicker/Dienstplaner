using Dienstplaner.Domain.Models;

namespace Dienstplaner.Application.Interfaces;

public interface IScheduleImporter
{
    Task<ImportResult> ImportAsync(string filePath);
}
