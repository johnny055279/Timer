using System.Collections.Generic;
using System.Threading.Tasks;
using Timer.Application.Models;

namespace Timer.Application.Interfaces;

public interface ILogUploader
{
    bool IsConfigured { get; }

    /// <summary>Uploads the entries and returns the report ID the user passes on to the developer.</summary>
    Task<string> UploadAsync(IReadOnlyList<LogEntry> entries);
}
