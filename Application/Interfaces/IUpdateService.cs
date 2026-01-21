using System.Threading;
using System.Threading.Tasks;
using Timer.Application.Models;

namespace Timer.Application.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken);
}
