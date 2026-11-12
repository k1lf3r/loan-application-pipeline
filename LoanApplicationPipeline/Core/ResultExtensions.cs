namespace LoanApplicationPipeline.Core;

public static class ResultExtensions
{
    public static (IReadOnlyList<T> Values, IReadOnlyList<E> Errors) Partition<T, E>(
        this IEnumerable<Result<T, E>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var values = new List<T>();
        var errors = new List<E>();

        foreach (var item in source)
        {
            switch (item)
            {
                case Result<T, E>.Ok ok:
                    values.Add(ok.Value);
                    break;
                case Result<T, E>.Err err:
                    errors.Add(err.Error);
                    break;
            }
        }

        return (values, errors);
    }
}
