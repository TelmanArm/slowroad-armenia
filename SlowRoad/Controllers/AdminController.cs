using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SlowRoad.Models;

namespace SlowRoad.Controllers;

[Authorize] 
public class AdminController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<AdminController> _log;

    // A throwaway hash, computed once, so that a login for an address with no
    // account costs the same PBKDF2 work as one for a real account. Without it
    // the response time says which address is the admin, whatever the error
    // message claims: the real-account path hashes, the no-account path
    // returns immediately.
    private static readonly string DecoyHash =
        new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), "decoy");

    public AdminController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ILogger<AdminController> log)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _log = log;
    }

    // GET /Admin → admin home (logged in only)
    public IActionResult Index() => View();

    // GET /Admin/Login → show form
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // POST /Admin/Login → check password
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            // Burn the work a real check would have cost, then fail the same way.
            _userManager.PasswordHasher.VerifyHashedPassword(
                new IdentityUser(), DecoyHash, model.Password);

            return InvalidLogin(model, "unknown address");
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: false, lockoutOnFailure: true);

        // Locked out gets the same answer as a bad password on purpose: a
        // distinct "account locked" message would confirm the address exists
        // and undo the timing fix above. The window is a minute, so a real
        // admin who waits gets straight back in.
        if (result.IsLockedOut)
            return InvalidLogin(model, "locked out");

        if (!result.Succeeded)
            return InvalidLogin(model, "wrong password");

        _log.LogInformation("Admin login succeeded for {Email} from {Ip}",
            user.Email, HttpContext.Connection.RemoteIpAddress);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction(nameof(Index));
    }

    // One message for every way a login can fail; the reason goes to the log,
    // which is also the only record anyone has that an attempt happened.
    private IActionResult InvalidLogin(LoginViewModel model, string reason)
    {
        _log.LogWarning("Admin login failed ({Reason}) for {Email} from {Ip}",
            reason, model.Email, HttpContext.Connection.RemoteIpAddress);

        ModelState.AddModelError("", "Invalid email or password.");
        return View(model);
    }

    // POST /Admin/Logout → delete cookie
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
