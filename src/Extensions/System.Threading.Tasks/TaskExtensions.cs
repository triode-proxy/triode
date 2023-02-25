namespace System.Threading.Tasks;

public static class TaskExtensions
{
    public static async Task WithTimeout(this Task task, TimeSpan timeout, CancellationToken canceled = default)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout, canceled)).ConfigureAwait(false))
            task.Wait(canceled);
        else if (canceled.IsCancellationRequested)
            throw new OperationCanceledException(canceled);
        else
            throw new TimeoutException();
    }

    public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken canceled = default)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout, canceled)).ConfigureAwait(false))
            return task.Result;
        else if (canceled.IsCancellationRequested)
            throw new OperationCanceledException(canceled);
        else
            throw new TimeoutException();
    }
}
