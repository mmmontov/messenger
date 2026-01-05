using System;
using System.Collections.Generic;

namespace Messenger.Client.Models;

public sealed record FileAttachment(int Id, string FilePath, string FileName, long FileSize, string MimeType);

public sealed record Message(
    int Id,
    int ChatId,
    int SenderId,
    string? ContentText,
    string MessageType,
    DateTime CreatedAt,
    DateTime? EditedAt,
    bool IsDeleted,
    IReadOnlyList<FileAttachment> Files
);


