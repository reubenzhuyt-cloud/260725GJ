using System;

public enum TimePhase
{
    Dawn,      // 黎明
    Daytime,   // 白昼
    Dusk,      // 黄昏
    Night      // 黑夜
}

[Serializable]
[System.Obsolete("Deprecated: Use GamePhaseManager instead")]
public class TimeState
{
    public int currentDay = 1;
    public int hour = 5;
    public int minute = 0;
    public TimePhase currentPhase = TimePhase.Dawn;

    public override string ToString()
    {
        return $"Day {currentDay} - {hour:D2}:{minute:D2} - {GetPhaseName(currentPhase)}";
    }

    public string GetTimeString()
    {
        return $"{hour:D2}:{minute:D2}";
    }

    public static string GetPhaseName(TimePhase phase)
    {
        switch (phase)
        {
            case TimePhase.Dawn:    return "黎明";
            case TimePhase.Daytime: return "白昼";
            case TimePhase.Dusk:    return "黄昏";
            case TimePhase.Night:   return "黑夜";
            default:                return "未知";
        }
    }
}