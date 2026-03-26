## Summary
- clarify that `InvisibleGorilla-TUN` remains a Windows-only companion service
- document that Android support belongs to `InvisibleGorilla-XRayClient` through an Android app head and `VpnService` path
- keep platform boundaries explicit while Android rollout continues in the client repository

## Why
`InvisibleGorilla-TUN` is tightly coupled to Windows networking, Wintun, and the desktop companion-process model. Android needs a different runtime based on `VpnService`, so keeping this repository Windows-focused avoids a misleading cross-platform contract.

## Test plan
- verify README clearly states that this repository is Windows-only
- verify Android references now point to `InvisibleGorilla-XRayClient`
- verify no Windows build or runtime behavior changed
