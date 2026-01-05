using System;

namespace Messenger.Client.Models;

public sealed record UserProfile(
    int Id,
    string Email,
    string Username,
    string? Bio,
    string? AvatarPath,
    string Status,
    DateTime CreatedAt,
    DateTime? LastSeenAt
);


