namespace MarkMello.Application.Updates;

public abstract record UpdatePrepareResult
{
    private UpdatePrepareResult()
    {
    }

    public sealed record Success(string Message) : UpdatePrepareResult;

    public sealed record Failed(string Message) : UpdatePrepareResult;
}
