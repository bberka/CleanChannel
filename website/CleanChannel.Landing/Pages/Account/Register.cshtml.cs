using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CleanChannel.Landing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CleanChannel.Landing.Pages.Account;

public sealed class RegisterModel(DemoLicenseService licenses) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = licenses.Register(Input.Email, Input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "That email is already registered or the input is not valid.");
            return Page();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Email),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToPage("/Account/Index");
    }

    public sealed class RegisterInput
    {
        [Required, EmailAddress, StringLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(128, MinimumLength = 12)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
