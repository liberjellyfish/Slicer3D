using System;
using System.Collections.Generic;
using UnityEngine;

public static class SdfSceneDriverUtility
{
    public static SdfPhase1Driver[] FindSurfaceDrivers(SdfPhase1Driver excludedDriver = null)
    {
        SdfPhase1Driver[] foundDrivers = UnityEngine.Object.FindObjectsByType<SdfPhase1Driver>(FindObjectsSortMode.None);
        if (foundDrivers == null || foundDrivers.Length <= 0)
        {
            return Array.Empty<SdfPhase1Driver>();
        }

        List<SdfPhase1Driver> surfaceDrivers = new List<SdfPhase1Driver>(foundDrivers.Length);
        for (int i = 0; i < foundDrivers.Length; i++)
        {
            SdfPhase1Driver driver = foundDrivers[i];
            if (driver == null || driver == excludedDriver || !driver.gameObject.scene.IsValid())
            {
                continue;
            }

            if (driver.GetComponent<SdfSharedVolumeProxy>() != null)
            {
                continue;
            }

            surfaceDrivers.Add(driver);
        }

        return surfaceDrivers.ToArray();
    }

    public static bool TryGetCombinedBounds(IReadOnlyList<SdfPhase1Driver> drivers, out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        if (drivers == null)
        {
            return false;
        }

        for (int i = 0; i < drivers.Count; i++)
        {
            SdfPhase1Driver driver = drivers[i];
            if (driver == null)
            {
                continue;
            }

            Bounds driverBounds = driver.GetWorldBounds();
            if (!hasBounds)
            {
                combinedBounds = driverBounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(driverBounds);
        }

        return hasBounds;
    }
}
