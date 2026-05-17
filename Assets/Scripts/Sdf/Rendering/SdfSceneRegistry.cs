using System;
using System.Collections.Generic;

public static class SdfSceneRegistry
{
    private static readonly List<SdfObjectRuntime> runtimes = new List<SdfObjectRuntime>();
    private static readonly List<SdfRaymarchDriver> drivers = new List<SdfRaymarchDriver>();
    private static readonly List<SdfRaymarchDriver> queryResults = new List<SdfRaymarchDriver>();
    private static int dataVersion = 1;
    private static int membershipVersion = 1;

    public static int Version => dataVersion;
    public static int MembershipVersion => membershipVersion;
    public static int RuntimeCount => CountValidRuntimes();
    public static int DriverCount => CountValidDrivers();

    public static void Register(SdfObjectRuntime runtime)
    {
        if (runtime == null || runtimes.Contains(runtime))
        {
            return;
        }

        runtimes.Add(runtime);
        IncrementMembershipVersion();
    }

    public static void Unregister(SdfObjectRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtimes.Remove(runtime))
        {
            IncrementMembershipVersion();
        }
    }

    public static void Register(SdfRaymarchDriver driver)
    {
        if (driver == null || drivers.Contains(driver))
        {
            return;
        }

        drivers.Add(driver);
        IncrementMembershipVersion();
    }

    public static void Unregister(SdfRaymarchDriver driver)
    {
        if (driver == null)
        {
            return;
        }

        if (drivers.Remove(driver))
        {
            IncrementMembershipVersion();
        }
    }

    public static void MarkMembershipDirty(SdfObjectRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        IncrementMembershipVersion();
    }

    public static void MarkDirty(SdfObjectRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        IncrementDataVersion();
    }

    public static void MarkDirty(SdfRaymarchDriver driver)
    {
        if (driver == null)
        {
            return;
        }

        IncrementDataVersion();
    }

    public static SdfRaymarchDriver[] FindSurfaceDrivers(SdfRaymarchDriver excludedDriver = null)
    {
        queryResults.Clear();
        AddRuntimeDrivers(excludedDriver);
        AddLegacyDrivers(excludedDriver);
        return queryResults.Count > 0 ? queryResults.ToArray() : Array.Empty<SdfRaymarchDriver>();
    }

    private static void AddRuntimeDrivers(SdfRaymarchDriver excludedDriver)
    {
        for (int i = runtimes.Count - 1; i >= 0; i--)
        {
            SdfObjectRuntime runtime = runtimes[i];
            if (runtime == null)
            {
                runtimes.RemoveAt(i);
                IncrementMembershipVersion();
                continue;
            }

            if (!runtime.ParticipatesInSceneSdf)
            {
                continue;
            }

            SdfRaymarchDriver driver = runtime.Driver;
            if (!SdfSceneDriverUtility.IsRenderableSurfaceDriver(driver, excludedDriver))
            {
                continue;
            }

            AddUniqueDriver(driver);
        }
    }

    private static void AddLegacyDrivers(SdfRaymarchDriver excludedDriver)
    {
        for (int i = drivers.Count - 1; i >= 0; i--)
        {
            SdfRaymarchDriver driver = drivers[i];
            if (driver == null)
            {
                drivers.RemoveAt(i);
                IncrementMembershipVersion();
                continue;
            }

            if (!SdfSceneDriverUtility.IsRenderableSurfaceDriver(driver, excludedDriver))
            {
                continue;
            }

            AddUniqueDriver(driver);
        }
    }

    private static void AddUniqueDriver(SdfRaymarchDriver driver)
    {
        if (driver != null && !queryResults.Contains(driver))
        {
            queryResults.Add(driver);
        }
    }

    private static int CountValidRuntimes()
    {
        int count = 0;
        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimes[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountValidDrivers()
    {
        int count = 0;
        for (int i = 0; i < drivers.Count; i++)
        {
            if (drivers[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static void IncrementMembershipVersion()
    {
        membershipVersion = IncrementVersionValue(membershipVersion);
        dataVersion = IncrementVersionValue(dataVersion);
    }

    private static void IncrementDataVersion()
    {
        dataVersion = IncrementVersionValue(dataVersion);
    }

    private static int IncrementVersionValue(int value)
    {
        unchecked
        {
            value++;
            return value > 0 ? value : 1;
        }
    }
}
