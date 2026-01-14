using System;
using System.Collections.Generic;

namespace Messenger.Client.Models;

public sealed record ChatMember(int UserId, string Username, string? AvatarPath, string? Bio, string Status, DateTime? LastSeenAt);

public sealed record Chat(string Id, DateTime CreatedAt, IReadOnlyList<ChatMember> Members);

public sealed record DialogListItem(Chat Chat, string? LastMessagePreview, DateTime? LastMessageAt)
{
    public ChatMember? GetOtherMember(int currentUserId)
    {
        return Chat.Members.FirstOrDefault(m => m.UserId != currentUserId);
    }
    
    public string GetOtherMemberUsername(int currentUserId)
    {
        var member = GetOtherMember(currentUserId);
        return member?.Username ?? "Unknown";
    }
    
    public string GetOtherMemberAvatar(int currentUserId)
    {
        var member = GetOtherMember(currentUserId);
        if (member is null || string.IsNullOrEmpty(member.Username)) return "?";
        return member.Username.Substring(0, 1).ToUpper();
    }
}


