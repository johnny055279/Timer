namespace Timer.Application.Interfaces;

public interface ICounterService
{
    int Count { get; }
    int Step { get; }

    void SetCount(int count);
    void SetStep(int step);
    void Increase();
    void IncreaseBy(int amount);
    void Decrease();
    void DecreaseBy(int amount);
    void Reset();
}
