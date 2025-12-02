using System.Text.Json.Serialization;
using Domain.Enums;

namespace Domain.Entities;

public class MxfProcess
{
    #region Props
    public Guid Id { get; private set; }
    public ProcessStatus Status { get; private set; }

    public string Path { get; private set; }
    public string OutputBlobPath { get; private set; } = null!;
    public long FileSize { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UploadCompletedAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? ProcessingCompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    #endregion

    #region ctors

    [JsonConstructor]
    internal MxfProcess(
        Guid id,
        ProcessStatus status,
        string path,
        string? outputBlobPath,
        long fileSize,
        DateTime createdAt,
        DateTime? uploadCompletedAt,
        DateTime? processingStartedAt,
        DateTime? processingCompletedAt,
        string? errorMessage)
    {
        Id = id;
        Status = status;
        FileSize = fileSize;
        CreatedAt = createdAt;
        UploadCompletedAt = uploadCompletedAt;
        ProcessingStartedAt = processingStartedAt;
        ProcessingCompletedAt = processingCompletedAt;
        ErrorMessage = errorMessage;
        Path = path;
        OutputBlobPath = outputBlobPath ?? "";
    }



    private MxfProcess(Guid id, string path)
    {
        Id = id;
        Status = ProcessStatus.Uploading;
        Path = path;
        CreatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Business Methods
    public static MxfProcess Create(Guid id, string inputBlobPath)
    {
        if (string.IsNullOrWhiteSpace(inputBlobPath))
            throw new ArgumentException("Input blob path cannot be empty.", nameof(inputBlobPath));

        return new MxfProcess(id, inputBlobPath);
    }

    public void MarkUploadCompleted(long size)
    {
        EnsureStateIs(ProcessStatus.Uploading);

        if (size <= 0)
            throw new InvalidOperationException("Uploaded file size must be greater than zero.");

        FileSize = size;
        UploadCompletedAt = DateTime.UtcNow;
        Status = ProcessStatus.Queued;
    }

    public void MarkQueued()
    {
        EnsureStateIs(ProcessStatus.Uploading, ProcessStatus.Queued);

        Status = ProcessStatus.Queued;
    }

    public void MarkProcessingStarted()
    {
        EnsureStateIs(ProcessStatus.Queued);

        ProcessingStartedAt = DateTime.UtcNow;
        Status = ProcessStatus.Processing;
    }

    public void MarkProcessingCompleted(string outputPath)
    {
        EnsureStateIs(ProcessStatus.Processing);

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be empty.");

        OutputBlobPath = outputPath;
        ProcessingCompletedAt = DateTime.UtcNow;
        Status = ProcessStatus.Completed;
    }

    public void MarkError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = "Unknown error.";

        ErrorMessage = message;
        Status = ProcessStatus.Error;
    }

    #endregion
    #region Validation Methods
    private void EnsureStateIs(params ProcessStatus[] allowed)
    {
        if (!allowed.Contains<ProcessStatus>(Status))
        {
            var allowedList = string.Join(", ", allowed);
            throw new InvalidOperationException(
                $"Cannot transition from state '{Status}' to the requested state. Allowed: {allowedList}");
        }
    }
    #endregion
}
