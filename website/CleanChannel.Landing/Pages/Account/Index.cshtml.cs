using System.Security.Claims;
using CleanChannel.Landing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CleanChannel.Landing.Pages.Account;

[Authorize]
public sealed class IndexModel(DemoLicenseService licenses) : PageModel
{
    public LicenseStatus Status { get; private set; } = new(null, null, false, null);

    public string? PurchaseCode { get; private set; }

    public string? Message { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet() => LoadStatus();

    public IActionResult OnPostPurchase(string plan)
    {
        if (!DemoPlanParser.TryParse(plan, out var selectedPlan))
        {
            ErrorMessage = "Choose a valid demo plan.";
            LoadStatus();
            return Page();
        }

        var receipt = licenses.IssuePurchase(CurrentUserId(), selectedPlan);
        if (receipt is null)
        {
            ErrorMessage = "The demo account could not be found.";
            LoadStatus();
            return Page();
        }

        PurchaseCode = receipt.ActivationCode;
        Message = $"Demo {receipt.Plan} purchase issued. Copy the activation code below, then activate it.";
        LoadStatus();
        return Page();
    }

    public IActionResult OnPostActivate(string activationCode)
    {
        if (!licenses.Activate(CurrentUserId(), activationCode))
        {
            ErrorMessage = "That activation code is invalid for this account.";
            LoadStatus();
            return Page();
        }

        Message = "License activated. The protected Pro demo artifact is now available.";
        LoadStatus();
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private void LoadStatus() => Status = licenses.GetStatus(CurrentUserId());
}
