using System;
using System.Collections.Generic;
using UnityEngine;

public static class SdfSceneDriverUtility
{
    public static bool IsRenderableSurfaceDriver(SdfRaymarchDriver driver, SdfRaymarchDriver excludedDriver = null)
    {
        if (driver == null || driver == excludedDriver)
        {
            return false;
        }

        GameObject driverObject = driver.gameObject;
        if (driverObject == null || !driverObject.scene.IsValid() || !driverObject.activeInHierarchy || !driver.isActiveAndEnabled)
        {
            return false;
        }

        if (driver.GetComponent<SdfSharedVolumeProxy>() != null)
        {
            return false;
        }

        Renderer renderer = driver.GetComponent<Renderer>();
        return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
    }

    public static SdfRaymarchDriver[] FindSurfaceDrivers(SdfRaymarchDriver excludedDriver = null)
    {
        SdfRaymarchDriver[] foundDrivers = UnityEngine.Object.FindObjectsByType<SdfRaymarchDriver>(FindObjectsSortMode.None);
        if (foundDrivers == null || foundDrivers.Length <= 0)
        {
            return Array.Empty<SdfRaymarchDriver>();
        }

        List<SdfRaymarchDriver> surfaceDrivers = new List<SdfRaymarchDriver>(foundDrivers.Length);
        for (int i = 0; i < foundDrivers.Length; i++)
        {
            SdfRaymarchDriver driver = foundDrivers[i];
            if (!IsRenderableSurfaceDriver(driver, excludedDriver))
            {
                continue;
            }

            surfaceDrivers.Add(driver);
        }

        return surfaceDrivers.ToArray();
    }

    public static bool TryGetCombinedBounds(IReadOnlyList<SdfRaymarchDriver> drivers, out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        if (drivers == null)
        {
            return false;
        }

        for (int i = 0; i < drivers.Count; i++)
        {
            SdfRaymarchDriver driver = drivers[i];
            if (!IsRenderableSurfaceDriver(driver))
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
