namespace MarkMello.Presentation.Views;

public partial class ViewerView
{
    // Кэш нужен только чтобы не поднимать событие, когда строка не изменилась; значение
    // для чтения всегда считается заново (хост мог подключиться уже после прокрутки).
    private int? _outlineCurrentLine;

    public int? CurrentSourceLine => _documentView?.GetCurrentHeadingSourceLine();

    private EventHandler? _currentSourceLineChanged;

    public event EventHandler? CurrentSourceLineChanged
    {
        add
        {
            _currentSourceLineChanged += value;
            // Новый подписчик читает значение сам; кэш сравнения синхронизируем с ним.
            _outlineCurrentLine = CurrentSourceLine;
        }
        remove => _currentSourceLineChanged -= value;
    }

    public bool TryNavigateToSourceLine(int sourceLine)
        => _documentView?.TryScrollToHeadingSourceLine(sourceLine) == true;

    private void UpdateOutlineCurrentLine()
    {
        // Rule 1 ADR-0008: пока панели нет, прокрутка и рендер не платят за обход заголовков.
        if (_currentSourceLineChanged is null)
        {
            return;
        }

        var line = _documentView?.GetCurrentHeadingSourceLine();
        if (line == _outlineCurrentLine)
        {
            return;
        }

        _outlineCurrentLine = line;
        _currentSourceLineChanged.Invoke(this, EventArgs.Empty);
    }
}
