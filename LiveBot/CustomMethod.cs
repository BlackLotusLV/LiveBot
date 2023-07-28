namespace LiveBot;

internal static class CustomMethod
{
    public static string ScoreToTime(int time)
    {
        StringBuilder[] sTime = { new(), new() };
        for (var i = 0; i < time.ToString().Length; i++)
        {
            if (i < time.ToString().Length - 3)
            {
                sTime[0].Append(time.ToString()[i]);
            }
            else
            {
                sTime[1].Append(time.ToString()[i]);
            }
        }

        if (sTime[0].Length == 0)
        {
            sTime[0].Append('0');
        }

        while (sTime[1].Length < 3)
        {
            sTime[1].Insert(0, '0');
        }

        TimeSpan seconds = TimeSpan.FromSeconds(double.Parse(sTime[0].ToString()));
        return seconds.Hours == 0 ? $"{seconds.Minutes}:{seconds.Seconds}.{sTime[1]}" : $"{seconds.Hours}:{seconds.Minutes}:{seconds.Seconds}.{sTime[1]}";
    }

    /// <summary>
    /// Checks if the user has the required permissions to use the command
    /// </summary>
    /// <param name="member"></param>
    /// <returns>If user has permissions, returns true</returns>
    public static bool CheckIfMemberAdmin(DiscordMember member)
    {
        return member.Permissions.HasPermission(Permissions.ManageMessages) ||
               member.Permissions.HasPermission(Permissions.KickMembers) ||
               member.Permissions.HasPermission(Permissions.BanMembers) ||
               member.Permissions.HasPermission(Permissions.Administrator);
    }
}