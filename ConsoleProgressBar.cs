public class ConsoleProgressBar
{
    private readonly int _totalSteps;
    private int _currentStep;
    private int _barSize;
    private char _progressCharacter;
    private string _customValue;

    public ConsoleProgressBar(int totalSteps, int barSize = 50, char progressCharacter = '■')
    {
        _totalSteps = totalSteps;
        _currentStep = 0;
        _barSize = barSize;
        _progressCharacter = progressCharacter;
        _customValue = string.Empty;
    }

    public void IncrementStep(string customValue = "")
    {
        _currentStep++;
        _customValue = customValue;
        DrawProgressBar();
    }

    private void DrawProgressBar()
    {
        Console.CursorLeft = 0;
        Console.Write("[");
        var progress = (int)(_currentStep / (double)_totalSteps * _barSize);
        for (int i = 0; i < _barSize; i++)
        {
            if (i < progress)
            {
                Console.Write(_progressCharacter);
            }
            else
            {
                Console.Write(' ');
            }
        }
        Console.Write($"] {_currentStep}/{_totalSteps} ({(int)(_currentStep / (double)_totalSteps * 100)}%) {_customValue}");
    }
}
