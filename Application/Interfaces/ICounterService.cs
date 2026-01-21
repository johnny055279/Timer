namespace Timer.Application.Interfaces;

public interface ICounterService
{
    int Count { get; }
    int Step { get; }

    void SetStep(int step);
    void Increase();
    void Decrease();
    void Reset();
}
