using System.Text.Json;
using LoanApplicationPipeline.Core;
using LoanApplicationPipeline.Models;

namespace LoanApplicationPipeline.Services;

public static class ApplicationDataLoader
{
    public static Result<IReadOnlyList<RawApplicationRecord>, DomainError> LoadFromJsonFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Failure(
                new DomainError.FileNotFound(filePath ?? string.Empty));

        try
        {
            if (!File.Exists(filePath))
                return Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Failure(
                    new DomainError.FileNotFound(filePath));

            string json = File.ReadAllText(filePath);
            var records = JsonSerializer.Deserialize<List<RawApplicationRecord>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return records is not null
                ? Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Success(records)
                : Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Failure(
                    new DomainError.MalformedFile(filePath, "JSON root resolved to null."));
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Failure(
                new DomainError.MalformedFile(filePath, ex.Message));
        }
        catch (IOException ex)
        {
            return Result<IReadOnlyList<RawApplicationRecord>, DomainError>.Failure(
                new DomainError.MalformedFile(filePath, ex.Message));
        }
    }
}