using System;
using System.Collections.Generic;

/// <summary>
/// 중앙 입력 잠금 관리자.
/// 기존 코드의 GameInputState.IsLocked = true/false 문법을 유지하면서도
/// 호출한 시스템별로 잠금을 분리하여 서로의 잠금을 잘못 해제하지 않게 한다.
/// </summary>
public static class GameInputState
{
    private static readonly HashSet<string> Locks = new HashSet<string>();
    private static int tokenSequence;

    public static bool IsLocked
    {
        get { return Locks.Count > 0; }
        set
        {
            string callerKey = GetLegacyCallerKey();
            if (value)
                Locks.Add(callerKey);
            else
                Locks.Remove(callerKey);
        }
    }

    public static int LockCount { get { return Locks.Count; } }

    public static IDisposable Acquire(string reason)
    {
        string safeReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim();
        string key = "Token:" + safeReason + ":" + (++tokenSequence);
        Locks.Add(key);
        return new LockToken(key);
    }

    public static void SetLocked(string reason, bool locked)
    {
        string key = "Reason:" + (string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim());
        if (locked)
            Locks.Add(key);
        else
            Locks.Remove(key);
    }

    public static bool IsLockedBy(string reason)
    {
        string key = "Reason:" + (string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim());
        return Locks.Contains(key);
    }

    public static void ClearAll()
    {
        Locks.Clear();
    }

    private static string GetLegacyCallerKey()
    {
        try
        {
            var trace = new System.Diagnostics.StackTrace();
            for (int i = 1; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i).GetMethod();
                var type = method != null ? method.DeclaringType : null;
                if (type == null || type == typeof(GameInputState))
                    continue;

                return "Legacy:" + type.FullName;
            }
        }
        catch
        {
            // 스택 추적을 사용할 수 없는 빌드에서는 공용 키로 안전하게 폴백한다.
        }

        return "Legacy:Unknown";
    }

    private sealed class LockToken : IDisposable
    {
        private string key;

        public LockToken(string newKey)
        {
            key = newKey;
        }

        public void Dispose()
        {
            if (string.IsNullOrEmpty(key))
                return;

            Locks.Remove(key);
            key = null;
        }
    }
}
