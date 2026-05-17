using System;
using System.Text;

namespace HC.Blazor.Pages.Chat1.Components;

public enum ChatInitialAvatarSize
{
    Sm24,
    Md30,
    Msg32,
    Lg36
}

/// <summary>
/// Pure Blazor initials avatar (replaces canvas/VoloChatAvatarManager).
/// </summary>
public static class ChatInitialAvatarHelper
{
    public static string GetHashSource(string? displayName, string? username)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            return username.Trim();
        }

        return "\0";
    }

    /// <summary>Same modulus hash as legacy JS avatar palette.</summary>
    public static int GetToneIndex(string? hashSource)
    {
        var s = hashSource ?? string.Empty;
        unchecked
        {
            var hash = 0;
            foreach (var c in s)
            {
                hash = c + ((hash << 5) - hash);
            }

            return Math.Abs(hash % 10);
        }
    }

    public static string GetToneClass(string? displayName, string? username)
        => $"chat-initial-tone-{GetToneIndex(GetHashSource(displayName, username))}";

    /// <summary>
    /// First letter of the last whitespace-separated word in display name, then the same rule on username.
    /// Examples: "Nguyễn Hồ Phi Long" → L, "Trần Việt Hùng" → H.
    /// </summary>
    public static string GetInitialLetter(string? displayName, string? username)
    {
        return TryLetterFromLastWord(displayName)
               ?? TryLetterFromLastWord(username)
               ?? "?";
    }

    /// <summary>Returns first letter/digit rune from the last whitespace-delimited token.</summary>
    private static string? TryLetterFromLastWord(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var segments = source.Trim().Split((char[]?)null!, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var lastWord = segments[^1];
        foreach (var rune in lastWord.EnumerateRunes())
        {
            if (Rune.IsLetter(rune))
            {
                return rune.ToString().ToUpperInvariant();
            }

            if (Rune.IsDigit(rune))
            {
                return rune.ToString();
            }
        }

        return null;
    }

    public static string SizeToCssClass(ChatInitialAvatarSize size) => size switch
    {
        ChatInitialAvatarSize.Sm24 => "chat-initial-avatar--sm",
        ChatInitialAvatarSize.Md30 => "chat-initial-avatar--md",
        ChatInitialAvatarSize.Msg32 => "chat-initial-avatar--msg",
        ChatInitialAvatarSize.Lg36 => "chat-initial-avatar--lg",
        _ => "chat-initial-avatar--sm"
    };
}
