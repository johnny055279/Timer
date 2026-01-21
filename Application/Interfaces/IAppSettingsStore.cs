using Timer.Application.Models;

namespace Timer.Application.Interfaces;

public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
