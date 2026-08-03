using Microsoft.Extensions.Logging;
using UnityAssetsPatcher.Application.IO;

namespace UnityAssetsPatcher.Infrastructure.IO;

internal static partial class IOLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Writing file {DestinationPath} with mode {DestinationMode}")]
    public static partial void WritingFile(ILogger logger, string destinationPath, FileDestinationMode destinationMode);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Wrote file {DestinationPath} with mode {DestinationMode}")]
    public static partial void FileWritten(ILogger logger, string destinationPath, FileDestinationMode destinationMode);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Copying file {SourcePath} to {DestinationPath} with mode {DestinationMode}")]
    public static partial void CopyingFile(
        ILogger logger,
        string sourcePath,
        string destinationPath,
        FileDestinationMode destinationMode);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Copied file {SourcePath} to {DestinationPath} with mode {DestinationMode}")]
    public static partial void FileCopied(
        ILogger logger,
        string sourcePath,
        string destinationPath,
        FileDestinationMode destinationMode);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Debug,
        Message = "Deleting file {FilePath}")]
    public static partial void DeletingFile(ILogger logger, string filePath);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Debug,
        Message = "Deleted file {FilePath}")]
    public static partial void FileDeleted(ILogger logger, string filePath);

    [LoggerMessage(
        EventId = 2010,
        Level = LogLevel.Debug,
        Message = "Ensuring directory {DirectoryPath} exists")]
    public static partial void EnsuringDirectory(ILogger logger, string directoryPath);

    [LoggerMessage(
        EventId = 2011,
        Level = LogLevel.Debug,
        Message = "Directory {DirectoryPath} is ready")]
    public static partial void DirectoryReady(ILogger logger, string directoryPath);

    [LoggerMessage(
        EventId = 2012,
        Level = LogLevel.Debug,
        Message = "Moving directory {SourcePath} to {DestinationPath}")]
    public static partial void MovingDirectory(ILogger logger, string sourcePath, string destinationPath);

    [LoggerMessage(
        EventId = 2013,
        Level = LogLevel.Debug,
        Message = "Moved directory {SourcePath} to {DestinationPath}")]
    public static partial void DirectoryMoved(ILogger logger, string sourcePath, string destinationPath);

    [LoggerMessage(
        EventId = 2014,
        Level = LogLevel.Debug,
        Message = "Deleting directory tree {DirectoryPath}")]
    public static partial void DeletingDirectoryTree(ILogger logger, string directoryPath);

    [LoggerMessage(
        EventId = 2015,
        Level = LogLevel.Debug,
        Message = "Deleted directory tree {DirectoryPath}")]
    public static partial void DirectoryTreeDeleted(ILogger logger, string directoryPath);

    [LoggerMessage(
        EventId = 2016,
        Level = LogLevel.Debug,
        Message = "Opening file {FilePath} for reading")]
    public static partial void OpeningFileForRead(ILogger logger, string filePath);

    [LoggerMessage(
        EventId = 2017,
        Level = LogLevel.Debug,
        Message = "Opened file {FilePath} for reading")]
    public static partial void FileOpenedForRead(ILogger logger, string filePath);

    [LoggerMessage(
        EventId = 2018,
        Level = LogLevel.Debug,
        Message = "Reading file attributes of {FilePath}")]
    public static partial void GettingAttributes(ILogger logger, string filePath);

    [LoggerMessage(
        EventId = 2019,
        Level = LogLevel.Debug,
        Message = "Read file attributes of {FilePath}: {Attributes}")]
    public static partial void AttributesRead(ILogger logger, string filePath, FileAttributes attributes);

    [LoggerMessage(
        EventId = 2020,
        Level = LogLevel.Debug,
        Message = "Failed to read file attributes of {FilePath}")]
    public static partial void GetAttributesFailed(ILogger logger, string filePath, Exception exception);

    [LoggerMessage(
        EventId = 2030,
        Level = LogLevel.Debug,
        Message = "Compressing stream with Brotli")]
    public static partial void Compressing(ILogger logger);

    [LoggerMessage(
        EventId = 2031,
        Level = LogLevel.Debug,
        Message = "Compressed stream with Brotli in {ElapsedMilliseconds} ms")]
    public static partial void Compressed(ILogger logger, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 2032,
        Level = LogLevel.Debug,
        Message = "Decompressing Brotli stream")]
    public static partial void Decompressing(ILogger logger);

    [LoggerMessage(
        EventId = 2033,
        Level = LogLevel.Debug,
        Message = "Decompressed Brotli stream in {ElapsedMilliseconds} ms")]
    public static partial void Decompressed(ILogger logger, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 2090,
        Level = LogLevel.Warning,
        Message = "Failed to delete temporary file {TemporaryPath} after a failed write")]
    public static partial void TemporaryFileCleanupFailed(ILogger logger, string temporaryPath, Exception exception);

    [LoggerMessage(
        EventId = 2091,
        Level = LogLevel.Warning,
        Message = "Committed file but failed to delete recovery file {RecoveryPath}")]
    public static partial void RecoveryFileCleanupFailed(ILogger logger, string recoveryPath, Exception exception);

    [LoggerMessage(
        EventId = 2092,
        Level = LogLevel.Debug,
        Message = "Failed to open file {FilePath} for reading")]
    public static partial void FileOpenForReadFailed(ILogger logger, string filePath, Exception exception);
}
