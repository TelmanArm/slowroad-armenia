namespace SlowRoad.Security;

// How hard /Admin/Login pushes back on password guessing. Bound from the
// "Security:Login" config section; the defaults here are what production runs
// on, and tests override them to exercise each control.
public class LoginSecurityOptions
{
    // Attempts allowed per client IP per window before the endpoint stops
    // answering at all. This is the control that makes an unlimited
    // brute-force run impossible, and it also keeps a flood of junk logins
    // from burning the box's CPU on password hashing.
    public int PermitLimit { get; set; } = 10;

    public double WindowMinutes { get; set; } = 5;

    // Failed attempts against one account before it is locked.
    public int MaxFailedAttempts { get; set; } = 10;

    // Deliberately short. With a single admin account, a long hard lock is its
    // own denial of service: anyone could hold the owner out of their own site
    // indefinitely. The per-IP limit above is what actually stops an attacker.
    public double LockoutMinutes { get; set; } = 1;

    public TimeSpan Window => TimeSpan.FromMinutes(WindowMinutes);

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);
}
