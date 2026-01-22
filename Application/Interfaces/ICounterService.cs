namespace Timer.Application.Interfaces;

public interface ICounterService
{
    int Count { get; }
    int Step { get; }

    void SetCount(int count);
    void SetStep(int step);
    void Increase();
    void Decrease();
    void Reset();
}
